using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace LayeredFilesMergerEngine
{
    public interface IEngine
    {
        void UseConfig(EngineConfig config);
        void Start();
        void SetLayerInclude(string key, bool include);

        Action<IList<LayerListDto>> LayerListRefresh { get; set; }
        Action<LayerListDto> LayerListAdd { get; set; }
        Action<string> LayerListDelete { get; set; }
        Action<string, LayerListDto> LayerListUpdate { get; set; }
        void MoveLayer(string sourceKey, string targetKey);
        IList<FileSysDto> GetLayerDetails(CancellationToken cancellationToken, string layerKey, string subPath = "");
        void ExploreLayersFolder();
    }

    public class Engine : IEngine
    {
        private EngineConfig _config;
        private FileSystemWatcher _packageWatcher;
        private readonly ILogger _logger;
        private readonly ILayerFactory _layerFactory;
        private readonly ILayerDetailsService _layerDetailsService;
        private Dictionary<string, Layer> _layers;
        private readonly ILayerNameInfoService _layerNameInfoService;

        public Engine(ILogger logger, ILayerFactory layerFactory, ILayerDetailsService layerDetailsService, ILayerNameInfoService layerNameInfoService)
        {
            _logger = logger;
            _layerFactory = layerFactory;
            _layerDetailsService = layerDetailsService;
            _layerNameInfoService = layerNameInfoService;

            _layers = new Dictionary<string, Layer>();
        }

        public void LoadConfig(string configFilePath)
        {
            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);

                _config = JsonConvert.DeserializeObject<EngineConfig>(json);
            }
            else
            {
                _config = new EngineConfig
                {

                };
            }
        }

        public void UseConfig(EngineConfig config)
        {
            _config = config;
        }

        public void Start()
        {
            _logger.Information("Engine start!");
            if (!Directory.Exists(_config.LayerPath))
            {
                Directory.CreateDirectory(_config.LayerPath);
            }

            GetLayers();

            var layerDtoList = _layers.Select(x => CreateLayerListDtoFromLayer(x.Key, x.Value))
                .OrderBy(x => x.Priority)
                .ToList();

            LayerListRefresh?.Invoke(layerDtoList);

            _packageWatcher = new FileSystemWatcher
            {
                Path = _config.LayerPath,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.LastAccess |
                               NotifyFilters.LastWrite |
                               NotifyFilters.FileName |
                               NotifyFilters.DirectoryName,
                IncludeSubdirectories = true
            };

            _packageWatcher.Changed += OnChanged;
            _packageWatcher.Created += OnChanged;
            _packageWatcher.Deleted += OnChanged;
            _packageWatcher.Renamed += OnRenamed;

            _packageWatcher.EnableRaisingEvents = true;
        }

        private LayerListDto CreateLayerListDtoFromLayer(string layerKey, Layer layer)
        {
            var layerListDto = new LayerListDto
            {
                Key = layerKey,
                Include = layer.Include,
                Priority = layer.Priority,
                Name = layer.Name,
                Version = layer.Version
            };

            return layerListDto;
        }

        public Action<IList<LayerListDto>> LayerListRefresh { get; set; }
        public Action<LayerListDto> LayerListAdd { get; set; }
        public Action<string> LayerListDelete { get; set; }
        public Action<string, LayerListDto> LayerListUpdate { get; set; }

        public void SetLayerInclude(string key, bool include)
        {
            var layer = _layers
                .Where(x => x.Key == key)
                .Select(y => y.Value)
                .Single();

            layer.Include = include;
            _layerDetailsService.SaveDetails(_config.LayerPath, key, layer);
        }

        private void GetLayers()
        {
            var packageDirs = Directory.GetDirectories(_config.LayerPath);
            var newLayers = new Dictionary<string, Layer>();
            foreach (var packageDir in packageDirs)
            {
                var getLayerResult = GetLayerFromDirectory(packageDir);
                if (getLayerResult.ResultType == GetLayerResultType.Old)
                {
                    AddLayer(getLayerResult.LayerKey, getLayerResult.Layer, saveDetails: false, lowestPrio: false);
                }
                else
                {
                    newLayers.Add(getLayerResult.LayerKey, getLayerResult.Layer);
                }
            }

            foreach (var layer in newLayers)
            {
                AddLayer(layer.Key, layer.Value, saveDetails: true, lowestPrio: true);
            }
        }

        private GetLayerResult GetLayerFromDirectory(string fullPath)
        {
            var dirInfo = new DirectoryInfo(fullPath);
            var layerKey = dirInfo.Name;
            _logger.Information("Found layer [{LayerKey}]", layerKey);
            var layer = _layerFactory.GetLayer(_config.LayerPath, layerKey);
            return layer;
        }

        private void AddLayer(string layerKey, Layer layer, bool saveDetails = true, bool lowestPrio = false, bool notify = false)
        {
            if (lowestPrio)
            {
                var maxPrio = _layers.Values.Count > 0 ? _layers.Values.Max(x => x.Priority) : 0;
                layer.Priority = maxPrio + 10000;
            }

            _logger.Information("Engine AddLayer [{LayerKey} - priority {Priority}]", layerKey, layer.Priority);

            if (saveDetails)
            {
                _layerDetailsService.SaveDetails(_config.LayerPath, layerKey, layer);
            }

            _layers.Add(layerKey, layer);

            if (notify)
            {
                LayerListAdd?.Invoke(CreateLayerListDtoFromLayer(layerKey, layer));
            }
        }

        private void RemoveLayer(string layerKey)
        {
            //var layer = _layers
            //    .Where(x => x.Key == layerKey)
            //    .Select(x => x.Value)
            //    .Single();

            _layers.Remove(layerKey);
            LayerListDelete?.Invoke(layerKey);
            _logger.Information("Engine RemoveLayer [{LayerKey}]", layerKey);
        }

        public void MoveLayer(string sourceKey, string targetKey)
        {
            var sourceLayer = _layers[sourceKey];
            var targetLayer = _layers[targetKey];
            var layersAfterTarget = _layers.Values
                .Where(x => x.Priority > targetLayer.Priority)
                .ToList();
            var nextPrioAfterTarget = layersAfterTarget.Count == 0 ?
                targetLayer.Priority + 10000 : 
                layersAfterTarget.Min(x => x.Priority);

            var sourceNewPrio = (targetLayer.Priority + nextPrioAfterTarget) / 2;

            _logger.Debug("Move [{SourceLayer}-{SourcePrio}] to [{TargetLayer}-{TargetPrio}-{NextPrioAfterTarget}] - source new prio: {SourceNewPrio}",
                sourceKey, sourceLayer.Priority,
                targetKey, targetLayer.Priority,
                nextPrioAfterTarget,
                sourceNewPrio);

            sourceLayer.Priority = sourceNewPrio;
            _layerDetailsService.SaveDetails(_config.LayerPath, sourceKey, sourceLayer);

            LayerListUpdate?.Invoke(sourceKey, CreateLayerListDtoFromLayer(sourceKey, sourceLayer));

            //_layers.Remove(sourceKey);

        }

        public IList<FileSysDto> GetLayerDetails(CancellationToken cancellationToken, string layerKey, string path)
        {
                var result = new List<FileSysDto>();
                
                var layerPath = Path.Combine(_config.LayerPath, layerKey);
                var fullFolderPath = Path.Combine(layerPath, path);
                var fileSystemEntries = Directory.GetFileSystemEntries(fullFolderPath);
                cancellationToken.ThrowIfCancellationRequested();

                //var layer = GetLayer(layerKey);
                //var pathParts = GetPathParts(path);
                //layer.EnFiles.Where()


                foreach (var fileSystemEntry in fileSystemEntries)
                {
                    _logger.Information("{FileSystgemEntry}", fileSystemEntry);
                    var fileInfo = new FileInfo(fileSystemEntry);
                    var fullPath = fileInfo.FullName.Substring(layerPath.Length+1);
                    var isDirectory = (fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                    var modified = isDirectory ? Directory.GetLastWriteTime(fileSystemEntry) : File.GetLastWriteTime(fileSystemEntry);
                    var length = isDirectory ? (long?) null : fileInfo.Length;
                    var dto = new FileSysDto
                    {
                        FileSystemPath = Path.Combine(fullFolderPath, fileInfo.Name),
                        FullPath = fullPath,
                        Name = fileInfo.Name,
                        Extension = fileInfo.Extension,
                        IsDirectory = isDirectory,
                        Size = length,
                        Modified = modified
                    };
                    //var dirInfo = new DirectoryInfo(fileSystemEntry);

                    result.Add(dto);
                }


                //var directoryInfo = new DirectoryInfo(layerPath);
                //var fullPath = new Uri(directoryInfo.Name);


                //CreateLayerDetailDto();
                return result;

        }

        private string[] GetPathParts(string path)
        {
            var parts = path.Split('/', '\\');
            return parts;
        }

        private FileSysDto CreateLayerDetailDto()
        {
            var dto = new FileSysDto
            {

            };

            return dto;
        }

        private Layer GetLayer(string layerKey)
        {
            var layer = _layers
                .Where(x => x.Key == layerKey)
                .Select(x => x.Value)
                .First();

            return layer;
        }

        public void ExploreLayersFolder()
        {
            System.Diagnostics.Process.Start(_config.LayerPath);
        }

        void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                    {
                        if (IsDirectory(e.FullPath) && IsInLayerDirectory(e.FullPath))
                        {
                            _logger.Information("New Package! {Path}", e.FullPath);
                            //var dirInfo = new DirectoryInfo(e.FullPath);
                            var getLayerResult = GetLayerFromDirectory(e.FullPath);
                            if (!_layers.ContainsKey(getLayerResult.LayerKey))
                            {
                                //_layers.Add(getLayerResult.LayerKey, getLayerResult.Layer);
                                AddLayer(getLayerResult.LayerKey, getLayerResult.Layer, saveDetails: true, lowestPrio: true, notify: true);
                            }
                            else
                            {
                                _logger.Warning("FileSystemWatcher Created: Found already existing Layer {LayerKey}. Ignoring!", getLayerResult.LayerKey);
                            }
                        }
                        break;
                    }
                    case WatcherChangeTypes.Deleted:
                    {
                        if (IsInLayerDirectory(e.FullPath))
                        {
                            _logger.Information("Deleted Package! {Path}", e.FullPath);
                            var layerKey = GetLayerKey(e.FullPath);
                            if (_layers.ContainsKey(layerKey))
                            {
                                RemoveLayer(layerKey);
                            }
                            else
                            {
                                _logger.Warning("FileSystemWatcher Deleted: Layer {LayerKey} already deleted? Ignoring!", layerKey);
                            }
                        }

                        break;
                    }
                    default:
                    {
                        _logger.Information("Engine.OnChanged: {ChangeType}: [{FullPath}]", e.ChangeType, e.FullPath);
                        break;
                    }
                }

            }
            catch (FileNotFoundException)
            {

            }
            catch(Exception exception)
            {
                _logger.Error(exception, "Error occured when handling FileSystem OnChanged!");
            }
        }

        void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Renamed:
                    {
                        if (IsDirectory(e.FullPath) && IsInLayerDirectory(e.FullPath))
                        {
                            var oldKey = GetLayerKey(e.OldFullPath);
                            var newKey = GetLayerKey(e.FullPath);

                            var layer = _layers[oldKey];
                            layer.Name = _layerNameInfoService.GetName(newKey);
                            layer.Version = _layerNameInfoService.GetVersion(newKey);

                            _layers.Remove(oldKey);
                            _layers.Add(newKey, layer);
                            LayerListUpdate?.Invoke(oldKey, CreateLayerListDtoFromLayer(newKey, layer));

                                //layer.IsDirty
                            }

                            break;
                    }
                    default:
                    {
                        _logger.Information("Engine.OnRenamed: {ChangeType}: [{FullPath}] ([{OldFullPath})]", e.ChangeType, e.FullPath, e.OldFullPath);
                        break;
                    }
                }
            }
            catch (FileNotFoundException)
            {

            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Error occured when handling FileSystem OnRenamed!");
            }
        }

        private string GetLayerKey(string fullPath)
        {
            var fileInfo = new FileInfo(fullPath);
            return fileInfo.Name;
        }

        private bool IsDirectory(string fullPath)
        {
            var isDirectory = (File.GetAttributes(fullPath) & FileAttributes.Directory) == FileAttributes.Directory;
            _logger.Debug("IsDirectory[{FullPath}]: {IsDirectory}", fullPath, isDirectory);
            return isDirectory;
        }

        private bool IsInLayerDirectory(string fullPath)
        {
            var isInLayerDirectory = new DirectoryInfo(fullPath).Parent.FullName == _config.LayerPath;
            _logger.Debug("IsInLayerDirectory[{FullPath}]: {IsInLayerDirectory}", fullPath, isInLayerDirectory);
            return isInLayerDirectory;
        }
    }

}

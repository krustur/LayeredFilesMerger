using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrightIdeasSoftware;
using LayeredFilesMergerEngine;
using Serilog;

namespace LayeredFilesMergerForm
{
    public interface IMainForm
    { }

    public partial class MainForm : Form, IMainForm
    {
        private readonly SynchronizationContext synchronizationContext;
        private readonly IEngine _engine;
        private readonly ILogger _logger;
        private bool _dataGridViewLayersDisableEvents;
        private SortableBindingList<LayerListDto> _layerList = new SortableBindingList<LayerListDto>();
        private ImageList _imageList;

        public MainForm(IEngine engine, ILogger logger)
        {
            synchronizationContext = SynchronizationContext.Current;
            _engine = engine;
            _logger = logger;
            InitializeComponent();

            //// ObjectListViewLayers
            ////objectListViewLayers.ShowGroups = false;
            //var incCol2 = new OLVColumn();
            //var keyCol2 = new OLVColumn();
            //var prioCol2 = new OLVColumn();
            //var nameCol2 = new OLVColumn();
            //var verCol2 = new OLVColumn();
            //objectListViewLayers.Columns.Add(incCol2);
            //objectListViewLayers.Columns.Add(keyCol2);
            //objectListViewLayers.Columns.Add(prioCol2);
            //objectListViewLayers.Columns.Add(nameCol2);
            //objectListViewLayers.Columns.Add(verCol2);

            //incCol2.CheckBoxes = true;
            //incCol2.Text = "Include";
            //incCol2.AspectName = "Include";

            //keyCol2.Text = "Key";
            //keyCol2.AspectName = "Key";
            //keyCol2.IsVisible = false;

            //prioCol2.Text = "Priority";
            //prioCol2.AspectName = "Priority";
            //prioCol2.IsVisible = false;

            //nameCol2.Text = "Name";
            //nameCol2.AspectName = "Name";

            //verCol2.Text = "Version";
            //verCol2.AspectName = "Version";

            ////objectListViewLayers.RebuildColumns();

            Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            TestIcons();

            _dataGridViewLayersDisableEvents = true;

            objectListViewLayers.Sort(olvPriority, SortOrder.Ascending);
            objectListViewLayers.CheckBoxes = true;
            objectListViewLayers.CheckedAspectName = "Include";
            objectListViewLayers.ItemChecked += objectListViewLayers_ItemChecked;

            //treeListView1.Sort(olvColumnIsDirectory, SortOrder.Ascending);
            //treeListView1.Sort(olvColumnName, SortOrder.Ascending);
            treeListView1.CustomSorter = delegate (OLVColumn column, SortOrder order) {
                treeListView1.ListViewItemSorter = new ColumnComparer(
                    olvColumnIsDirectory, SortOrder.Descending, column, order);
            };
            treeListView1.Sort();

            treeListView1.CanExpandGetter = delegate (object x) {
                var fileSystemDto = (FileSysDto)x;
                return fileSystemDto.IsDirectory;
            };

            treeListView1.ChildrenGetter = delegate (object x) {
                var fileSystemDto = (FileSysDto)x;
                var layer = objectListViewLayers.SelectedItem?.RowObject as LayerListDto;
                var result = _engine.GetLayerDetails(CancellationToken.None, layer.Key, fileSystemDto.FullPath);
                
                return result;
            };

            //var columnHeader = treeListView1.Columns["Size"];
            olvColumnSize.AspectToStringConverter = delegate (object x) 
            {
                return ((long?)x).GetSizeString();
            };
            this.olvColumnName.ImageGetter = delegate (object row) {
                var fileSystemDto = ((FileSysDto)row);
                var key = fileSystemDto.IsDirectory ? "no_extension_dir" : string.IsNullOrWhiteSpace(fileSystemDto.Extension) ? "no_extension_file" : fileSystemDto.Extension;

                if (fileSystemDto.IsDirectory == false && !this.treeListView1.SmallImageList.Images.ContainsKey(key))
                {
                    var icon = Icon.ExtractAssociatedIcon(fileSystemDto.FileSystemPath);
                    this.treeListView1.SmallImageList.Images.Add(key, icon);
                }
                return key;
            };

            var config = new EngineConfig
            {
                LayerPath = @"E:\Amiga\KrustWB2\Layers",
                OutputPath = @"E:\Amiga\KrustWB2\Output",
            };


            _engine.UseConfig(config);
            _engine.LayerListRefresh += OnLayerListRefresh;
            _engine.LayerListAdd += OnLayerListAdd;
            _engine.LayerListDelete += OnLayerListDelete;
            _engine.LayerListUpdate += OnLayerListUpdate;
            _engine.Start();

            _dataGridViewLayersDisableEvents = false;
        }

       

        private void TestIcons()
        {
            _imageList = new ImageList();
            listView1.SmallImageList = _imageList;
            listView1.View = View.SmallIcon;

            var dir = new System.IO.DirectoryInfo(@"D:\Download");

            listView1.BeginUpdate();

            // For each file in the c:\ directory, create a ListViewItem
            // and set the icon to the icon extracted from the file.
            foreach (var file in dir.GetFiles())
            {
                // Set a default icon for the file.

                var item = new ListViewItem(file.Name, 1);

                // Check to see if the image collection contains an image
                // for this extension, using the extension as a key.
                if (!_imageList.Images.ContainsKey(file.Extension))
                {
                    // If not, add the image to the image list.
                    var iconForFile = Icon.ExtractAssociatedIcon(file.FullName);
                    _imageList.Images.Add(file.Extension, iconForFile);
                }
                item.ImageKey = file.Extension;
                listView1.Items.Add(item);
            }
            listView1.EndUpdate();

        }


        private void OnLayerListRefresh(IList<LayerListDto> layerList)
        {
            _layerList = new SortableBindingList<LayerListDto>(layerList);
            
            objectListViewLayers.SetObjects(_layerList);

        }

        private void OnLayerListAdd(LayerListDto layerListDto)
        {
                _layerList.Add(layerListDto);
            
            objectListViewLayers.BuildList();
        }

        private void OnLayerListDelete(string layerKey)
        {
            var layerListDto = _layerList
                .Single(x => x.Key == layerKey);
            _layerList.Remove(layerListDto);

            objectListViewLayers.BuildList();
        }

        private void OnLayerListUpdate(string oldKey, LayerListDto layerListDto)
        {
            var currentLayerListDto = _layerList
                .Single(x => x.Key == oldKey);
            _layerList.Remove(currentLayerListDto);
            _layerList.Add(layerListDto);

            objectListViewLayers.BuildList();
            objectListViewLayers.Sort(olvPriority, SortOrder.Ascending);
        }

        private void objectListViewLayers_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            var item = (LayerListDto)((OLVListItem) e.Item).RowObject;
            var key =  item.Key;
            var included = e.Item.Checked;
            _engine.SetLayerInclude(key, included);
        }

        private CancellationTokenSource _cts;
        private void objectListViewLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
            _cts = new CancellationTokenSource();
            try
            {
                var layer = objectListViewLayers.SelectedItem?.RowObject as LayerListDto;
                if (layer != null)
                {

                    synchronizationContext.Post(x =>
                    {
                        //label1.Text = @"Counter ";
                        //label1.Text = "GetLayerDetails() in progress!";
                    }, null);

                    var details = _engine.GetLayerDetails(_cts.Token, layer.Key);
                    treeListView1.SetObjects(details);
                    //treeListView1.Sort(olvColumnIsDirectory, SortOrder.Ascending);
                    //treeListView1.Sort(olvColumnName, SortOrder.Ascending);
                    treeListView1.Sort();
                    //label1.Text = "-";
                    _cts = null;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Information("GetLayerDetails() cancelled!");
            }
        }

        private void objectListViewLayers_ModelDropped(object sender, ModelDropEventArgs e)
        {
            if (e.SourceListView == objectListViewLayers)
            {
                var targetLayer = (LayerListDto) e.TargetModel;
                var sourceLayer = (LayerListDto)e.SourceModels[0];
                _engine.MoveLayer(sourceLayer.Key, targetLayer.Key);
            }
        }

        private void objectListViewLayers_ModelCanDrop(object sender, ModelDropEventArgs e)
        {
            if (e.SourceModels == objectListViewLayers)
            {
                e.Effect = DragDropEffects.None;
            }
            else
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void menuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _engine.ExploreLayersFolder();
        }
    }
}

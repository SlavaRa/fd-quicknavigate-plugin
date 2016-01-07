using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ASCompletion;
using ASCompletion.Completion;
using ASCompletion.Context;
using ASCompletion.Model;
using PluginCore;
using PluginCore.Helpers;
using PluginCore.Managers;
using PluginCore.Utilities;
using ProjectManager;
using QuickNavigate.Forms;
using QuickNavigate.Helpers;
using WeifenLuo.WinFormsUI.Docking;

namespace QuickNavigate
{
    public class PluginMain : IPlugin
	{
        string settingFilename;
	    ControlClickManager controlClickManager;
	    ToolStripMenuItem typeExplorerItem;
	    ToolStripMenuItem quickOutlineItem;
        ToolStripMenuItem classHierarchyItem;
        ToolStripMenuItem editorClassHierarchyItem;

        #region Required Properties

        /// <summary>
        /// Api level of the plugin
        /// </summary>
        public int Api => 1;

        /// <summary>
        /// Name of the plugin
        /// </summary>
        public string Name => nameof(QuickNavigate);

        /// <summary>
        /// GUID of the plugin
        /// </summary>
        public string Guid => "5e256956-8f0d-4f2b-9548-08673c0adefd";

        /// <summary>
        /// Author of the plugin
        /// </summary> 
        public string Author => "Canab, SlavaRa";

        /// <summary>
        /// Description of the plugin
        /// </summary>
        public string Description => "QuickNavigate plugin";

        /// <summary>
        /// Web address for help
        /// </summary>
        public string Help => "http://www.flashdevelop.org/community/";

        /// <summary>
        /// Object that contains the settings
        /// </summary>
        [Browsable(false)]
        public object Settings { get; private set; }
		
		#endregion
		
		#region Required Methods
		
		/// <summary>
		/// Initializes the plugin
		/// </summary>
		public void Initialize()
		{
            InitBasics();
            LoadSettings();
            AddEventHandlers();
            CreateMenuItems();
            UpdateMenuItems();
            if (((Settings) Settings).CtrlClickEnabled) controlClickManager = new ControlClickManager();
        }
		
		/// <summary>
		/// Disposes the plugin
		/// </summary>
		public void Dispose()
		{
		    controlClickManager?.Dispose();
		    classHierarchyItem?.Dispose();
		    editorClassHierarchyItem?.Dispose();
            SaveSettings();
		}
		
		/// <summary>
		/// Handles the incoming events
		/// </summary>
		public void HandleEvent(object sender, NotifyEvent e, HandlingPriority priority)
		{
            switch (e.Type)
            {
                case EventType.UIStarted:
                    ASComplete.OnResolvedContextChanged += OnResolvedContextChanged;
                    UpdateMenuItems();
                    break;
                case EventType.FileSwitch:
                    if (controlClickManager != null) controlClickManager.Sci = PluginBase.MainForm.CurrentDocument.SciControl;
                    break;
                case EventType.Command:
                    if (((DataEvent)e).Action == ProjectManagerEvents.Project)
                    {
                        #region TODO slavara: ModelExplorer.current not updated after the change of the current project
                        ModelsExplorer.Instance.UpdateTree();
                        UpdateMenuItems();
                        #endregion
                    }
                    break;
            }
		}

        #endregion
        
        #region Custom Methods
       
        /// <summary>
        /// Initializes important variables
        /// </summary>
        void InitBasics()
        {
            string dataPath = Path.Combine(PathHelper.DataDir, Name);
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
            settingFilename = Path.Combine(dataPath, "Settings.fdb");
        }

        /// <summary>
        /// Loads the plugin settings
        /// </summary>
        void LoadSettings()
        {
            Settings = new Settings();
            if (!File.Exists(settingFilename)) SaveSettings();
            else Settings = (Settings)ObjectSerializer.Deserialize(settingFilename, Settings);
        }

        /// <summary>
        /// Adds the required event handlers
        /// </summary>
        void AddEventHandlers() => EventManager.AddEventHandler(this, EventType.UIStarted | EventType.FileSwitch | EventType.Command);

        /// <summary>
        /// Creates the required menu items
        /// </summary>
        void CreateMenuItems()
        {
            ToolStripMenuItem menu = (ToolStripMenuItem)PluginBase.MainForm.FindMenuItem("SearchMenu");
            Image image = PluginBase.MainForm.FindImage("99|16|0|0");
            typeExplorerItem = new ToolStripMenuItem("Type Explorer", image, ShowTypeExplorer, Keys.Control | Keys.Shift | Keys.R);
            PluginBase.MainForm.RegisterShortcutItem($"{Name}.TypeExplorer", typeExplorerItem);
            menu.DropDownItems.Add(typeExplorerItem);
            image = PluginBase.MainForm.FindImage("315|16|0|0");
            quickOutlineItem = new ToolStripMenuItem("Quick Outline", image, ShowQuickOutline, Keys.Control | Keys.Shift | Keys.O);
            PluginBase.MainForm.RegisterShortcutItem($"{Name}.Outline", quickOutlineItem);
            menu.DropDownItems.Add(quickOutlineItem);
            image = PluginBase.MainForm.FindImage("99|16|0|0");
            classHierarchyItem = new ToolStripMenuItem("Class Hierarchy", image, ShowClassHierarchy);
            menu.DropDownItems.Add(classHierarchyItem);
            editorClassHierarchyItem = new ToolStripMenuItem("Class Hierarchy", image, ShowClassHierarchy);
            PluginBase.MainForm.EditorMenu.Items.Insert(8, editorClassHierarchyItem);
            ToolStripMenuItem item = new ToolStripMenuItem("Recent Files", null, ShowRecentFiles, Keys.Control | Keys.E);
            PluginBase.MainForm.RegisterShortcutItem($"{Name}.RecentFiles", item);
            menu.DropDownItems.Add(item);
            item = new ToolStripMenuItem("Recent Projects", null, ShowRecentProjets);
            PluginBase.MainForm.RegisterShortcutItem($"{Name}.RecentProjects", item);
            menu.DropDownItems.Add(item);
        }

        /// <summary>
        /// Updates the state of the menu items
        /// </summary>
        void UpdateMenuItems()
        {
            typeExplorerItem.Enabled = PluginBase.CurrentProject != null;
            quickOutlineItem.Enabled = ASContext.Context.CurrentModel != null;
            bool canShowClassHierarchy = GetCanShowClassHierarchy();
            classHierarchyItem.Enabled = canShowClassHierarchy;
            editorClassHierarchyItem.Enabled = canShowClassHierarchy;
        }

        /// <summary>
        /// Saves the plugin settings
        /// </summary>
        void SaveSettings() => ObjectSerializer.Serialize(settingFilename, Settings);

        void ShowRecentFiles(object sender, EventArgs e)
        {
            OpenRecentFilesForm form = new OpenRecentFilesForm((Settings) Settings);
            if (form.ShowDialog() != DialogResult.OK) return;
            ProjectManager.PluginMain plugin = (ProjectManager.PluginMain) PluginBase.MainForm.FindPlugin("30018864-fadd-1122-b2a5-779832cbbf23");
            foreach (string it in form.SelectedItems)
            {
                plugin.OpenFile(it);
            }
        }

        void ShowRecentProjets(object sender, EventArgs e)
        {
            OpenRecentProjectsForm form = new OpenRecentProjectsForm((Settings) Settings);
            if (form.ShowDialog() != DialogResult.OK) return;
            string file = PluginBase.CurrentProject.GetAbsolutePath(form.SelectedItem);
            ProjectManager.PluginMain plugin = (ProjectManager.PluginMain) PluginBase.MainForm.FindPlugin("30018864-fadd-1122-b2a5-779832cbbf23");
            plugin.OpenFile(file);
        }

        void ShowTypeExplorer(object sender, EventArgs e)
        {
            if (PluginBase.CurrentProject == null) return;
            TypeExplorer form = new TypeExplorer((Settings) Settings);
            form.GotoPositionOrLine += OnGotoPositionOrLine;
            form.ShowInQuickOutline += ShowQuickOutline;
            form.ShowInClassHierarchy += ShowClassHierarchy;
            form.ShowInProjectManager += ShowInProjectManager;
            form.ShowInFileExplorer += ShowInFileExplorer;
            if (form.ShowDialog() != DialogResult.OK) return;
            TypeNode node = form.SelectedNode;
            if (node == null) return;
            FormHelper.Navigate(node.Model.InFile.FileName, node);
        }

        void ShowQuickOutline(object sender, EventArgs e)
        {
            if (ASContext.Context.CurrentModel == null) return;
            QuickOutline form = new QuickOutline(ASContext.Context.CurrentModel, (Settings) Settings);
            form.ShowInClassHierarchy += ShowClassHierarchy;
            if (form.ShowDialog() != DialogResult.OK) return;
            if (form.InFile == null) FormHelper.Navigate(form.InClass.InFile.FileName, form.SelectedNode);
            else FormHelper.Navigate(form.SelectedNode);
        }

        void ShowQuickOutline(Form sender, ClassModel model)
        {
            sender.Close();
            ((Control) PluginBase.MainForm).BeginInvoke((MethodInvoker) delegate
            {
                QuickOutline form = new QuickOutline(model, (Settings) Settings);
                form.ShowInClassHierarchy += ShowClassHierarchy;
                if (form.ShowDialog() != DialogResult.OK) return;
                if (form.InFile == null) FormHelper.Navigate(form.InClass.InFile.FileName, form.SelectedNode);
                else FormHelper.Navigate(form.SelectedNode);
            });
        }

        void ShowClassHierarchy(object sender, EventArgs e)
        {
            if (!GetCanShowClassHierarchy()) return;
            ClassModel curClass = ASContext.Context.CurrentClass;
            ShowClassHierarchy(!curClass.IsVoid() ? curClass : ASContext.Context.CurrentModel.GetPublicClass());
        }

        void ShowClassHierarchy(Form sender, ClassModel model)
        {
            sender.Close();
            ((Control) PluginBase.MainForm).BeginInvoke((MethodInvoker) delegate
            {
                ShowClassHierarchy(model);
            });
        }

        void ShowClassHierarchy(ClassModel model)
        {
            ClassHierarchy form = new ClassHierarchy(model, (Settings) Settings);
            form.GotoPositionOrLine += OnGotoPositionOrLine;
            form.ShowInQuickOutline += ShowQuickOutline;
            form.ShowInClassHierarchy += ShowClassHierarchy;
            form.ShowInProjectManager += ShowInProjectManager;
            form.ShowInFileExplorer += ShowInFileExplorer;
            if (form.ShowDialog() != DialogResult.OK) return;
            TypeNode node = form.SelectedNode;
            if (node == null) return;
            FormHelper.Navigate(node.Model.InFile.FileName, new TreeNode(node.Name) { Tag = node.Tag });
        }

        static bool GetCanShowClassHierarchy()
        {
            if (PluginBase.CurrentProject == null) return false;
            ITabbedDocument document = PluginBase.MainForm.CurrentDocument;
            if (document == null || !document.IsEditable) return false;
            IASContext context = ASContext.Context;
            return context != null && context.Features.hasExtends && (!context.CurrentClass.IsVoid() || !context.CurrentModel.GetPublicClass().IsVoid());
        }

        static void OnGotoPositionOrLine(Form sender, ClassModel model)
        {
            sender.Close();
            ((Control)PluginBase.MainForm).BeginInvoke((MethodInvoker)delegate
            {
                ModelsExplorer.Instance.OpenFile(model.InFile.FileName);
                PluginBase.MainForm.CallCommand("GoTo", null);
            });
        }

        static void ShowInProjectManager(Form sender, ClassModel model)
        {
            sender.Close();
            ((Control) PluginBase.MainForm).BeginInvoke((MethodInvoker) delegate
            {
                foreach (DockPane pane in PluginBase.MainForm.DockPanel.Panes)
                {
                    foreach (DockContent content in pane.Contents)
                    {
                        if (content.GetPersistString() != "30018864-fadd-1122-b2a5-779832cbbf23") continue;
                        foreach (ProjectManager.PluginUI ui in content.Controls.OfType<ProjectManager.PluginUI>())
                        {
                            content.Show();
                            ui.Tree.Select(model.InFile.FileName);
                            return;
                        }
                    }
                }
            });
        }

        static void ShowInFileExplorer(Form sender, ClassModel model)
        {
            sender.Close();
            ((Control) PluginBase.MainForm).BeginInvoke((MethodInvoker) delegate
            {
                foreach (DockPane pane in PluginBase.MainForm.DockPanel.Panes)
                {
                    foreach (DockContent content in pane.Contents)
                    {
                        if (content.GetPersistString() != "f534a520-bcc7-4fe4-a4b9-6931948b2686") continue;
                        foreach (FileExplorer.PluginUI ui in content.Controls.OfType<FileExplorer.PluginUI>())
                        {
                            ui.BrowseTo(Path.GetDirectoryName(model.InFile.FileName));
                            content.Show();
                            return;
                        }
                    }
                }
            });
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Cursor position changed and word at this position was resolved
        /// </summary>
        void OnResolvedContextChanged(ResolvedContext resolved) => UpdateMenuItems();

        #endregion
	}
}
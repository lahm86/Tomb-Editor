﻿using DarkUI.Controls;
using DarkUI.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TombIDE.Shared;
using TombLib.Projects;

namespace TombIDE.ProjectMaster
{
	public partial class SectionPluginList : UserControl
	{
		private IDE _ide;

		/// <summary>
		/// A list made up of plugins which were already installed in the current project on launch.
		/// </summary>
		private readonly List<Plugin> initialPlugins = new List<Plugin>();

		#region Initialization

		public SectionPluginList()
		{
			InitializeComponent();
		}

		public void Initialize(IDE ide)
		{
			_ide = ide;
			_ide.IDEEventRaised += OnIDEEventRaised;

			UpdateTreeView();

			tabControl.HideTab(1); // The "Description" tab

			initialPlugins.AddRange(_ide.Project.InstalledPlugins);
		}

		#endregion Initialization

		#region Events

		private void OnIDEEventRaised(IIDEEvent obj)
		{
			if (obj is IDE.PluginListsUpdatedEvent)
				UpdateTreeView();
		}

		private void button_ManagePlugins_Click(object sender, EventArgs e)
		{
			using (FormPluginManager form = new FormPluginManager(_ide))
			{
				form.ShowDialog(this);

				bool newPluginsInstalled = false;

				foreach (Plugin plugin in _ide.Project.InstalledPlugins)
				{
					if (initialPlugins.Exists(x => x.InternalDllPath.ToLower() == plugin.InternalDllPath.ToLower()))
						continue;

					_ide.AddPluginToLanguageFile(plugin);
					newPluginsInstalled = true;
				}

				if (newPluginsInstalled)
					_ide.RaiseEvent(new IDE.NewPluginsInstalledEvent());
			}
		}

		private void button_OpenInExplorer_Click(object sender, EventArgs e)
		{
			Plugin selectedPlugin = (Plugin)treeView.SelectedNodes[0].Tag;
			SharedMethods.OpenFolderInExplorer(Path.GetDirectoryName(selectedPlugin.InternalDllPath));
		}

		private void treeView_SelectedNodesChanged(object sender, EventArgs e)
		{
			button_OpenInExplorer.Enabled = treeView.SelectedNodes.Count > 0;

			if (treeView.SelectedNodes.Count > 0)
				UpdatePluginInfoOverview();
		}

		#endregion Events

		#region Methods

		private void UpdateTreeView()
		{
			treeView.SelectedNodes.Clear();
			treeView.Nodes.Clear();

			foreach (Plugin plugin in _ide.Project.InstalledPlugins)
			{
				DarkTreeNode node = new DarkTreeNode
				{
					Text = plugin.Name,
					Tag = plugin
				};

				treeView.Nodes.Add(node);
			}

			treeView.Invalidate();
		}

		private void UpdatePluginInfoOverview()
		{
			label_NoInfo.Visible = false;

			Plugin selectedPlugin = (Plugin)treeView.SelectedNodes[0].Tag;

			textBox_Title.Text = selectedPlugin.Name;

			if (string.IsNullOrEmpty(selectedPlugin.InternalDllPath))
			{
				textBox_DLLName.Text = Path.GetFileName(treeView.SelectedNodes[0].Text);
				button_OpenInExplorer.Enabled = false;

				label_NoLogo.Visible = true;

				if (tabControl.SelectedTab.Text == "Description")
					tabControl.SelectedIndex = 0;

				richTextBox_Description.Text = string.Empty;
				tabControl.HideTab(1); // The "Description" tab
			}
			else
			{
				textBox_DLLName.Text = Path.GetFileName(selectedPlugin.InternalDllPath);
				button_OpenInExplorer.Enabled = true;

				try
				{
					bool logoFound = false;

					foreach (string file in Directory.GetFiles(Path.GetDirectoryName(selectedPlugin.InternalDllPath)))
					{
						string extension = Path.GetExtension(file).ToLower();

						if (extension == ".jpg" || extension == ".png" || extension == ".bmp" || extension == ".gif")
						{
							panel_Logo.BackgroundImage = Image.FromFile(file);
							logoFound = true;

							break;
						}
					}

					if (!logoFound)
					{
						panel_Logo.BackgroundImage = null;
						label_NoLogo.Visible = true;
					}
					else
						label_NoLogo.Visible = false;

					string descriptionFilePath = Path.Combine(
						Path.GetDirectoryName(selectedPlugin.InternalDllPath),
						Path.GetFileNameWithoutExtension(selectedPlugin.InternalDllPath) + ".txt");

					if (File.Exists(descriptionFilePath))
					{
						richTextBox_Description.Text = File.ReadAllText(descriptionFilePath, Encoding.GetEncoding(1252));
						tabControl.ShowTab(1); // The "Description" tab
					}
					else
					{
						if (tabControl.SelectedTab.Text == "Description")
							tabControl.SelectedIndex = 0;

						tabControl.HideTab(1); // The "Description" tab
					}
				}
				catch (Exception ex)
				{
					DarkMessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		#endregion Methods
	}
}
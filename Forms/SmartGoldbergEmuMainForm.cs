/* Copyright (C) 2019-2020 Nemirtingas
   This file is part of the SmartGoldbergEmu Launcher

   The SmartGoldbergEmu Launcher is free software; you can redistribute it and/or
   modify it under the terms of the GNU Lesser General Public
   License as published by the Free Software Foundation; either
   version 3 of the License, or (at your option) any later version.

   The SmartGoldbergEmu Launcher is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
   Lesser General Public License for more details.

   You should have received a copy of the GNU Lesser General Public
   License along with the SmartGoldbergEmu Launcher; if not, see
   <http://www.gnu.org/licenses/>.
 */
using OSUtility;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmartGoldbergEmu
{
    public partial class SmartGoldbergEmuMainForm : Form
    {
        private ImageList _image_list = new ImageList();
        SavedConf crnilo = new SavedConf();
        private string[] dragndrop_files;

        private void RefreshAppListView()
        {
            // Don't clear the image list - preserve existing images
            app_list_view.Items.Clear();

            var apps = SteamEmulator.Apps;
            if (_isAlphabeticalSortEnabled)
                apps = apps.OrderBy(app => app.AppName, StringComparer.CurrentCultureIgnoreCase).ToList();

            foreach (var app in apps)
            {
                // Only load image if it doesn't already exist
                string key = app.GameGuid.ToString();
                if (!_image_list.Images.ContainsKey(key))
                {
                    LoadImage(app);
                }

                ListViewItem item = new ListViewItem
                {
                    Text = app.AppName,
                    ImageKey = key, // Use GameGuid as key
                    Tag = app
                };
                app_list_view.Items.Add(item);
            }
        }
        private void MigrateExistingEntries()
        {
            bool needsMigration = SteamEmulator.Apps.Any(app => app.GameGuid == Guid.Empty);

            if (needsMigration)
            {
                SteamEmulator.EnsureAllGamesHaveGuids();
                MessageBox.Show("Migrated existing entries to use GUIDs.", "Migration Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshAppListView();
            }
        }

        public SmartGoldbergEmuMainForm()
        {
            InitializeComponent();

            _image_list.ImageSize = new Size(32, 32);

            app_list_view.LargeImageList = _image_list;
            app_list_view.SmallImageList = _image_list;

            /*foreach (GameConfig app in SteamEmulator.Apps)
            {
                LoadImage(app);
                AddAppToList(app);
            }*/

            _isAlphabeticalSortEnabled = Properties.Settings.Default.SortAlphabetically;
            sortToggleButton.Checked = _isAlphabeticalSortEnabled;
            MigrateExistingEntries();
            RefreshAppListView();
        }

        private void AddGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AskGamePath();
        }

        private void EditGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditGame();
        }

        private void DeleteGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteGame();
        }

        private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditSettings();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void App_list_view_MouseDoubleClick(object sender, EventArgs e)
        {
            if (app_list_view.FocusedItem == null)
                return;
            GameConfig app = app_list_view.FocusedItem.Tag as GameConfig;
            if (app != null)
                SteamEmulator.StartGame(app);
        }

        public void LoadImage(GameConfig app)
        {
            try
            {
                string key = app.GameGuid.ToString();

                // Always remove and re-add to ensure fresh image
                if (_image_list.Images.ContainsKey(key))
                {
                    _image_list.Images.RemoveByKey(key);
                }

                if (string.IsNullOrEmpty(app.CustomIcon))
                {
                    if (File.Exists(app.Path))
                    {
                        Image result = Icon.ExtractAssociatedIcon(app.Path).ToBitmap();
                        _image_list.Images.Add(key, result);
                    }
                    else
                    {
                        // Use a default icon if file doesn't exist
                        _image_list.Images.Add(key, SystemIcons.Application);
                    }
                }
                else if (File.Exists(app.CustomIcon))
                {
                    Bitmap bmp = new Bitmap(app.CustomIcon);
                    _image_list.Images.Add(key, bmp);
                }
                else
                {
                    // Fallback to default icon
                    _image_list.Images.Add(key, SystemIcons.Application);
                }
            }
            catch (Exception ex)
            {
                // Use default icon on error
                string key = app.GameGuid.ToString();
                if (!_image_list.Images.ContainsKey(key))
                {
                    _image_list.Images.Add(key, SystemIcons.Application);
                }
            }
        }

        private delegate void AddAppToListDelegate(GameConfig app);

        private void AddAppToList(GameConfig app)
        {
            ListViewItem item = new ListViewItem
            {
                Text = app.AppName,
                ImageKey = app.GameGuid.ToString(),
                Tag = app
            };

            /*if (app_list_view.InvokeRequired)
                app_list_view.Invoke(new AddAppToListDelegate(AddAppToList), new object[] { app });
            else*/
                app_list_view.Items.Add(item);
        }

        private void AskGamePath()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = (OSDetector.IsWindows() ? "Game executables (*.exe)|*.exe;|All Files|*.*" : "All Files|*.*"),
                FilterIndex = 1,
                Multiselect = false,
                CheckFileExists = true
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                AddGame(openFileDialog.FileName);
            }
        }

        private void AddGame(string game_path)
        {
            GameConfig app = new GameConfig
            {
                Path = game_path,
            };

            do
            {
                app.GameGuid = Guid.NewGuid();
            } while (SteamEmulator.Apps.Any(a => a.GameGuid == app.GameGuid));

            app.AppName = Path.GetFileNameWithoutExtension(app.Path);
            app.StartFolder = Path.GetDirectoryName(game_path);

            using (GameSettingsForm gsform = new GameSettingsForm())
            {
                gsform.SetApp(app);
                DialogResult res = gsform.ShowDialog();
                if (res != DialogResult.OK)
                    return;

                app = gsform.Modified_app;

                if (app.GameGuid == Guid.Empty)
                {
                    app.GameGuid = Guid.NewGuid();
                }

                SteamEmulator.AddGame(app);

                LoadImage(app);
                // Remove this line: AddAppToList(app);

                if (!string.IsNullOrWhiteSpace(SteamEmulator.Config.webapi_key))
                {
                    string game_emu_folder = app.GetGameEmuFolder();
                    if (!File.Exists(Path.Combine(game_emu_folder, "steam_settings", "achievements.json")))
                    {
                        this.Enabled = false;
                        SteamEmulator.GenerateGameAchievements(app);
                        this.Enabled = true;
                    }
                }
            }
            RefreshAppListView(); // This will add the item properly
        }

        private void EditGame()
        {
            if (app_list_view.FocusedItem == null)
                return;

            GameConfig app = app_list_view.FocusedItem.Tag as GameConfig;
            if (app == null)
                return;

            // Find by GameGuid instead of Path since multiple entries can have same path
            int index = SteamEmulator.Apps.FindIndex(a => a.GameGuid == app.GameGuid);
            if (index == -1)
            {
                MessageBox.Show("Game not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (GameSettingsForm gsform = new GameSettingsForm())
            {
                gsform.SetApp(app);
                DialogResult res = gsform.ShowDialog();
                if (res != DialogResult.OK)
                    return;

                var modifiedApp = gsform.Modified_app;

                // Preserve the GameGuid
                modifiedApp.GameGuid = app.GameGuid;

                SteamEmulator.SetGame(index, modifiedApp);

                // Update the list view item
                app_list_view.FocusedItem.Text = modifiedApp.AppName;
                app_list_view.FocusedItem.ImageKey = modifiedApp.GameGuid.ToString();
                app_list_view.FocusedItem.Tag = modifiedApp;

                // Reload the image if custom icon changed
                if (app.CustomIcon != modifiedApp.CustomIcon)
                {
                    LoadImage(modifiedApp);
                }

                if (!string.IsNullOrWhiteSpace(SteamEmulator.Config.webapi_key))
                {
                    string game_emu_folder = modifiedApp.GetGameEmuFolder();
                    if (!File.Exists(Path.Combine(game_emu_folder, "steam_settings", "achievements.json")))
                    {
                        this.Enabled = false;
                        SteamEmulator.GenerateGameAchievements(modifiedApp);
                        this.Enabled = true;
                    }
                }
            }
        }

        /*private void DeleteGame()
        {
            int index = app_list_view.Items.IndexOf(app_list_view.FocusedItem);
            if (index == -1)
                return;
            app_list_view.Items.Remove(app_list_view.Items[index]);
            SteamEmulator.RemoveGame(SteamEmulator.Apps[index]);
            RefreshAppListView();
        }*/
        private void DeleteGame()
        {
            if (app_list_view.FocusedItem == null)
                return;

            GameConfig app = app_list_view.FocusedItem.Tag as GameConfig;
            if (app == null)
                return;

            // Find by GameGuid instead of index
            int index = SteamEmulator.Apps.FindIndex(a => a.GameGuid == app.GameGuid);
            if (index == -1)
                return;

            if (MessageBox.Show($"Are you sure you want to delete '{app.AppName}'?", "Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Remove from SteamEmulator by GameGuid
                SteamEmulator.RemoveGameByGuid(app.GameGuid);

                // Remove from list view
                app_list_view.Items.Remove(app_list_view.FocusedItem);

                // Optionally remove the image from image list
                string key = app.GameGuid.ToString();
                if (_image_list.Images.ContainsKey(key))
                {
                    _image_list.Images.RemoveByKey(key);
                }
            }
        }

        private void EditSettings()
        {
            SettingsForm sform = new SettingsForm
            {
                Config = SteamEmulator.Config
            };
            sform.ShowDialog();

            if (sform.DialogResult == DialogResult.OK)
            {
                SteamEmulator.Config = sform.Config;

                SteamEmulator.Save();
                SteamEmulator.SetupEmu(new GameConfig());
            }
        }

        private void App_list_view_Click(object sender, MouseEventArgs me)
        {
            if (me.Button == MouseButtons.Right)
            {
                if (app_list_view.FocusedItem.Bounds.Contains(me.Location))
                {
                    capp_contextMenuStrip.Show(Cursor.Position);
                }
            }
        }

        private void PropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditGame();
        }

        private void RemoveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete this appid?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                DeleteGame();
            }     
        }

        private void GenerateAchievementsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = app_list_view.Items.IndexOf(app_list_view.FocusedItem);
            if (index == -1)
                return;

            this.Enabled = false;
            SteamEmulator.GenerateGameAchievements(SteamEmulator.Apps[index]);
            this.Enabled = true;
        }

        private void GenerateItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = app_list_view.Items.IndexOf(app_list_view.FocusedItem);
            if (index == -1)
                return;

            this.Enabled = false;
            SteamEmulator.GenerateGameItems(SteamEmulator.Apps[index]);
            this.Enabled = true;
        }

        private void OpenGameEmuFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = app_list_view.Items.IndexOf(app_list_view.FocusedItem);
            if (index == -1)
                return;

            SteamEmulator.ShowGameEmuFolder(SteamEmulator.Apps[index]);
        }

        private delegate void EnableFormDelegate(bool enable);

        private void EnableForm(bool enable)
        {
            if (this.InvokeRequired)
                this.Invoke(new EnableFormDelegate(EnableForm), new object[] { enable });
            else
                this.Enabled = enable;

        }

        private void App_list_view_DragDrop(object sender, DragEventArgs e)
        {
            EnableForm(false);

            dragndrop_files = (string[])e.Data.GetData(DataFormats.FileDrop);

            Task.Factory.StartNew(() =>
            {
                foreach (string file in dragndrop_files)
                {
                    AddGame(file);
                }
            })
                .ContinueWith(t => EnableForm(true));
        }

        private void App_list_view_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void App_list_view_KeyUp(object sender, KeyEventArgs e)
        {
            if( e.KeyCode == Keys.Delete )
                DeleteGame();
        }

        private void SmartGoldbergEmuMainForm_SizeChanged(object sender, EventArgs e)
        {
            app_list_view.Size = this.ClientSize;
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm aboutf = new AboutForm();
            aboutf.ShowDialog();
        }

        private void SmartGoldbergEmuMainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SteamEmulator.RestoreSteam();
        }

        private void CreateShortcutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = app_list_view.Items.IndexOf(app_list_view.FocusedItem);
            if (index == -1)
                return;

            SteamEmulator.CreateShortcut(SteamEmulator.Apps[index]);
        }

        private void Zatvaranje(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.F1State = this.WindowState;
            if (this.WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.F1Location = this.Location;
                Properties.Settings.Default.F1Size = this.Size;
            }
            else
            {
                Properties.Settings.Default.F1Location = this.RestoreBounds.Location;
                Properties.Settings.Default.F1Size = this.RestoreBounds.Size;
            }
            Properties.Settings.Default.Save();
        }

        private void Otvaranje(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.F1Size.Width == 0) Properties.Settings.Default.Upgrade();
            if (Properties.Settings.Default.F1Size.Width == 0 || Properties.Settings.Default.F1Size.Height == 0)
            {
            }
            else
            {
                this.WindowState = Properties.Settings.Default.F1State;
                if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
                this.Location = Properties.Settings.Default.F1Location;
                this.Size = Properties.Settings.Default.F1Size;
            }
        }
        private void SortToggleButton_Click(object sender, EventArgs e)
        {
            _isAlphabeticalSortEnabled = sortToggleButton.Checked;
            Properties.Settings.Default.SortAlphabetically = _isAlphabeticalSortEnabled;
            Properties.Settings.Default.Save();
            RefreshAppListView();
        }
    }
}

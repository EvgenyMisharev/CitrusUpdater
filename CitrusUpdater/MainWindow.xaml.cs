/*
 * Copyright (c) <2023> <Misharev Evgeny>
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 * 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer 
 *    in the documentation and/or other materials provided with the distribution.
 * 3. Neither the name of the <organization> nor the names of its contributors may be used to endorse or promote products derived 
 *    from this software without specific prior written permission.
 * 4. Redistributions are not allowed to be sold, in whole or in part, for any compensation of any kind.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, 
 * BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
 * IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
 * OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; 
 * OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * Contact: <citrusbim@gmail.com> or <https://web.telegram.org/k/#@MisharevEvgeny>
 */

using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Forms = System.Windows.Forms;
using System.Collections.Generic;
using System.Xml;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CitrusUpdater
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon notifyIcon;
        private string updateCheckButtonName;
        public Forms.Timer timer;

        public static Dictionary<string, bool> RunCreateFoldersVersions;
        public static Dictionary<string,bool> RunDownloadFilesVersions;

        public MainWindow()
        {
            RunCreateFoldersVersions = new Dictionary<string, bool>();
            RunDownloadFilesVersions = new Dictionary<string, bool>();
            InitializeComponent();
        }
        private void CitrusUpdaterWPF_Loaded(object sender, RoutedEventArgs e)
        {
            // Показываем главное окно приложения
            Rect trayRect = SystemParameters.WorkArea;
            double left = trayRect.Right - this.Width;
            double top = trayRect.Bottom - this.Height;

            this.Topmost = true;
            this.Left = left;
            this.Top = top;
            this.Width = this.Width;
            this.Height = this.Height;

            // Скрываем главное окно приложения
            this.Hide();

            // Создаем иконку в трее
            this.notifyIcon = new TaskbarIcon();
            this.notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/citrus.ico"));
            this.notifyIcon.ToolTipText = "CITRUS для Revit";

            this.notifyIcon.ContextMenu = new System.Windows.Controls.ContextMenu();
            MenuItem mi_Open = new MenuItem();
            mi_Open.Header = "Открыть";
            mi_Open.Click += MenuItem_Open_Click;
            this.notifyIcon.ContextMenu.Items.Add(mi_Open);

            MenuItem mi_Exit = new MenuItem();
            mi_Exit.Header = "Выход";
            mi_Exit.Click += MenuItem_Exit_Click;
            this.notifyIcon.ContextMenu.Items.Add(mi_Exit);

            // Добавляем обработчик клика по иконке
            this.notifyIcon.TrayMouseDoubleClick += notifyIcon_TrayMouseDoubleClick;

            //Проверка сохраненныъ настроек
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "CitrusUpdaterSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("CitrusUpdater.exe", fileName);

            if (File.Exists(assemblyPath))
            {
                using (FileStream fs = new FileStream(assemblyPath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(string));
                    updateCheckButtonName = xSer.Deserialize(fs) as string;
                    fs.Close();
                }
            }

            if (updateCheckButtonName == "radioButton_EachTenMinutes")
            {
                radioButton_EachTenMinutes.IsChecked = true;
            }
            else if (updateCheckButtonName == "radioButton_EachHour")
            {
                radioButton_EachHour.IsChecked = true;
            }
            else
            {
                radioButton_OnLoad.IsChecked = true;
            }
            Process process = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Contains("Revit"));
            if(process == null)
            {
                CheckForUpdates();
            }

            timer = new Forms.Timer();
            if (updateCheckButtonName != null)
            {
                if (updateCheckButtonName == "radioButton_EachTenMinutes")
                {
                    timer.Interval = 10 * 10 * 1000;
                    timer.Tick += new EventHandler(Timer_Tick);
                }
                else if (updateCheckButtonName == "radioButton_EachHour")
                {
                    timer.Interval = 60 * 60 * 1000;
                    timer.Tick += new EventHandler(Timer_Tick);
                }
            }
            timer.Start();
        }
        private void notifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            // Показываем главное окно приложения
            Rect trayRect = SystemParameters.WorkArea;
            double left = trayRect.Right - this.Width;
            double top = trayRect.Bottom - this.Height;

            this.Topmost = true;
            this.Left = left;
            this.Top = top;
            this.Width = this.Width;
            this.Height = this.Height;
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }
        private void CitrusUpdaterWPF_Closed(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Скрываем главное окно приложения
            this.Hide();

            // Отменяем закрытие приложения
            e.Cancel = true;
        }

        //Заменить на добавление записи через установщик
        //private void CitrusUpdaterWPF_SourceInitialized(object sender, EventArgs e)
        //{
        //    // Получаем дескриптор окна приложения
        //    IntPtr handle = new WindowInteropHelper(this).Handle;

        //    // Получаем путь к файлу приложения
        //    string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

        //    // Добавляем приложение в автозапуск Windows
        //    Microsoft.Win32.Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", "CitrusUpdater", path);
        //}
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            // Показываем главное окно приложения
            Rect trayRect = SystemParameters.WorkArea;
            double left = trayRect.Right - this.Width;
            double top = trayRect.Bottom - this.Height;

            this.Topmost = true;
            this.Left = left;
            this.Top = top;
            this.Width = this.Width;
            this.Height = this.Height;

            // Показываем главное окно приложения
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            // Закрываем приложение
            this.notifyIcon.Dispose();
            this.Close();
            Application.Current.Shutdown();
        }
        private void RadioButtonUpdate_Checked(object sender, RoutedEventArgs e)
        {
            updateCheckButtonName = (this.groupBox_UpdateCheck.Content as System.Windows.Controls.Grid)
                .Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked.Value == true)
                .Name;

            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "CitrusUpdaterSettings.xml";
            string assemblyPath = assemblyPathAll.Replace("CitrusUpdater.exe", fileName);

            if (File.Exists(assemblyPath))
            {
                File.Delete(assemblyPath);
            }

            using (FileStream fs = new FileStream(assemblyPath, FileMode.Create))
            {
                XmlSerializer xSer = new XmlSerializer(typeof(string));
                xSer.Serialize(fs, updateCheckButtonName);
                fs.Close();
            }

            if(timer != null)
            {
                if (updateCheckButtonName == "radioButton_EachTenMinutes")
                {
                    timer.Stop();
                    timer = new Forms.Timer();
                    timer.Interval = 10 * 10 * 1000;
                    timer.Tick += new EventHandler(Timer_Tick);
                    timer.Start();
                }
                else if (updateCheckButtonName == "radioButton_EachHour")
                {
                    timer.Stop();
                    timer = new Forms.Timer();
                    timer.Interval = 60 * 60 * 1000;
                    timer.Tick += new EventHandler(Timer_Tick);
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                }
            }
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            timer = new Forms.Timer();
            Process process = Process.GetProcesses().FirstOrDefault(p => p.ProcessName.Contains("Revit"));
            if (process == null)
            {
                CheckForUpdates();
            }
            if (updateCheckButtonName == "radioButton_EachTenMinutes")
            {
                timer.Interval = 10 * 10 * 1000;
                timer.Tick += new EventHandler(Timer_Tick);
            }
            else if (updateCheckButtonName == "radioButton_EachHour")
            {
                timer.Interval = 60 * 60 * 1000;
                timer.Tick += new EventHandler(Timer_Tick);
            }
            timer.Start();
        }
        private async void CheckForUpdates()
        {
            string addinsDataURLString = "http://citrusbim.com/addinsdata.xml";

            if (CheckURL(addinsDataURLString))
            {
                string username = Environment.UserName;
                XmlDocument addinsDataXML = new XmlDocument();
                addinsDataXML.Load(addinsDataURLString);

                XmlNode yearsNode = addinsDataXML.SelectSingleNode("addinsinfo/years[@name='Yars']");
                List<string> yearsList = GetYearsList(yearsNode);
                XmlNode root = addinsDataXML.SelectSingleNode("addinsinfo/folder[@name='Addins']");
                XmlNode filesForDel = addinsDataXML.SelectSingleNode("addinsinfo/foldersfordel[@name='ForDel']");


                foreach (string year in yearsList)
                {
                    if(!RunCreateFoldersVersions.ContainsKey(year) 
                        || (RunCreateFoldersVersions.ContainsKey(year) && RunCreateFoldersVersions[year] == false))
                    {
                        await Task.Run(() => CreateFolders(root, year, username));
                    }
                    
                    if(!RunDownloadFilesVersions.ContainsKey(year) 
                        || (RunDownloadFilesVersions.ContainsKey(year) && RunDownloadFilesVersions[year] == false))
                    {
                        await Task.Run(() => DownloadFiles(root, year, username));
                    }
                }

                await Task.Run(() => DeleteFoldersAndFiles(filesForDel, username));

                string changelogURLString = "http://citrusbim.com/changelog.xml";
                if (CheckURL(changelogURLString))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(changelogURLString);
                    XmlNodeList changeList = xmlDoc.SelectNodes("/changelog/change");

                    // Очищаем содержимое TextBox
                    textBox_Info.Clear();

                    // Перебираем элементы <change> и выводим информацию в TextBox
                    foreach (XmlNode changeNode in changeList)
                    {
                        string number = changeNode.SelectSingleNode("number").InnerText;
                        string description = changeNode.SelectSingleNode("description").InnerText;

                        textBox_Info.AppendText("Номер изменения: " + number + Environment.NewLine);
                        textBox_Info.AppendText(description + Environment.NewLine);
                        textBox_Info.AppendText(Environment.NewLine);
                    }
                }
                else
                {
                    textBox_Info.Clear();
                    textBox_Info.AppendText("Отсутствует подключение к серверу!");
                }
            }
            else
            {
                textBox_Info.Clear();
                textBox_Info.AppendText("Отсутствует подключение к серверу!");
            }
        }
        static List<string> GetYearsList(XmlNode yearsNode)
        {
            List<string> tmpYearsList = new List<string>();
            if (yearsNode.HasChildNodes)
            {
                foreach (XmlNode child in yearsNode.ChildNodes)
                {
                    tmpYearsList.Add(child.Attributes["name"].Value);
                }
            }
            return tmpYearsList;
        }
        static bool CheckURL(string url)
        {
            WebRequest request = WebRequest.Create(url);
            request.Method = "HEAD";

            try
            {
                using (var response = request.GetResponse())
                    return ((HttpWebResponse)response).StatusCode == HttpStatusCode.OK;
            }
            catch
            {
                return false;
            }
        }
        static DateTime GetLastModifiedDateTime(string uri)
        {
            WebRequest request = WebRequest.Create(uri);
            request.Method = "HEAD";

            using (WebResponse response = request.GetResponse())
            {
                string lastModifiedStr = response.Headers.Get("Last-Modified");

                if (!string.IsNullOrEmpty(lastModifiedStr))
                {
                    return DateTime.Parse(lastModifiedStr).ToLocalTime();
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }
        static void CreateFolders(XmlNode node, string year, string username)
        {
            if (!RunCreateFoldersVersions.ContainsKey(year))
            {
                RunCreateFoldersVersions.Add(year,true);
            }
            else if (RunCreateFoldersVersions.ContainsKey(year))
            {
                RunCreateFoldersVersions[year] = true;
            }

            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "folder")
                    {
                        string folderName = child.Attributes["name"].Value;
                        string targetPath = child.Attributes["target_path"].Value.Replace("Year", year).Replace("%username%", username);

                        if (!Directory.Exists(targetPath))
                        {
                            Directory.CreateDirectory(targetPath);
                        }
                        CreateFolders(child, year, username);
                    }
                }
            }
            RunCreateFoldersVersions[year] = false;
        }
        static void DownloadFiles(XmlNode node, string year, string username)
        {
            if (!RunDownloadFilesVersions.ContainsKey(year))
            {
                RunDownloadFilesVersions.Add(year, true);
            }
            else if (RunDownloadFilesVersions.ContainsKey(year))
            {
                RunDownloadFilesVersions[year] = true;
            }
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "file")
                    {
                        string fileName = child.InnerText;
                        string fileType = child.Attributes["type"].Value;
                        string downloadUrl = child.Attributes["download_url"].Value.Replace("Year", year);
                        string targetPath = child.Attributes["target_path"].Value.Replace("Year", year).Replace("%username%", username);
                        DateTime lastModifiedDateTime = GetLastModifiedDateTime(downloadUrl);

                        if (!File.Exists(targetPath))
                        {
                            using (WebClient client = new WebClient())
                            {
                                client.DownloadFileAsync(new Uri(downloadUrl), targetPath);
                            }
                        }
                        else if (File.Exists(targetPath))
                        {
                            DateTime createDateTime = new FileInfo(targetPath).CreationTime;
                            if (lastModifiedDateTime > createDateTime)
                            {
                                using (WebClient client = new WebClient())
                                {
                                    client.DownloadFileAsync(new Uri(downloadUrl), targetPath);
                                }
                            }
                        }
                    }
                    DownloadFiles(child, year, username);
                }
            }
            RunDownloadFilesVersions[year] = false;
        }
        static void DeleteFoldersAndFiles(XmlNode node, string username)
        {
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "folder")
                    {
                        string folderName = child.Attributes["name"].Value;
                        string targetPath = child.Attributes["target_path"].Value.Replace("%username%", username);

                        if (Directory.Exists(targetPath))
                        {
                            Directory.Delete(targetPath, true);
                        }
                    }

                    else if (child.Name == "file")
                    {
                        string fileName = child.InnerText;
                        string fileType = child.Attributes["type"].Value;
                        string targetPath = child.Attributes["target_path"].Value.Replace("%username%", username);

                        if (File.Exists(targetPath))
                        {
                            try
                            {
                                File.Delete(targetPath);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            }
        }
        private void image_CitrusLogo_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string url = "https://www.citrusbim.com/";
            Process.Start(url);
        }
    }
}

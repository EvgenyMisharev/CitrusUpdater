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
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace CitrusUpdater
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon notifyIcon;
        private string updateCheckButtonName;
        public Forms.Timer timer;
        private string lastSuccessfulChange;
        private const string ChangeHistoryFileName = "changelog.xml";
        private const string LastChangeFileName = "LastChange.txt";

        public static ConcurrentDictionary<string, bool> RunCreateFoldersVersions;
        public static ConcurrentDictionary<string, bool> RunDownloadFilesVersions;

        private static readonly HttpClient httpClient;

        static MainWindow()
        {
            httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // Настройте тайм-аут по вашему усмотрению
            };

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
        }

        public MainWindow()
        {
            RunCreateFoldersVersions = new ConcurrentDictionary<string, bool>();
            RunDownloadFilesVersions = new ConcurrentDictionary<string, bool>();
            InitializeComponent();
        }

        private async void CitrusUpdaterWPF_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureTrayIcon();
            HideMainWindow();

            // Load settings
            LoadSettings();
            LoadLastSuccessfulChange();
            LoadChangeHistory();

            // Start the timer based on settings
            StartUpdateTimer();

            // Check for updates if Revit is not running
            if (!IsRevitRunning())
            {
                await CheckForUpdates();
            }
            else
            {
                ShowRevitRunningMessage();
            }
        }
        private void ConfigureTrayIcon()
        {
            // Setup tray icon
            this.notifyIcon = new TaskbarIcon
            {
                IconSource = new BitmapImage(new Uri("pack://application:,,,/Resources/citrus.ico")),
                ToolTipText = "CITRUS для Revit"
            };

            // Setup context menu
            var contextMenu = new ContextMenu();
            var mi_Open = new MenuItem { Header = "Открыть" };
            mi_Open.Click += MenuItem_Open_Click;
            contextMenu.Items.Add(mi_Open);

            var mi_Exit = new MenuItem { Header = "Выход" };
            mi_Exit.Click += MenuItem_Exit_Click;
            contextMenu.Items.Add(mi_Exit);

            this.notifyIcon.ContextMenu = contextMenu;
            this.notifyIcon.TrayMouseDoubleClick += notifyIcon_TrayMouseDoubleClick;
        }
        private void HideMainWindow()
        {
            this.Topmost = true;
            this.Left = SystemParameters.WorkArea.Right - this.Width;
            this.Top = SystemParameters.WorkArea.Bottom - this.Height;
            this.Hide();
        }
        private void LoadSettings()
        {
            string settingsFilePath = GetSettingsFilePath();
            if (File.Exists(settingsFilePath))
            {
                using (FileStream fs = new FileStream(settingsFilePath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(string));
                    updateCheckButtonName = xSer.Deserialize(fs) as string;
                }

                SetUpdateRadioButton();
            }
        }
        private string GetSettingsFilePath()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = "CitrusUpdaterSettings.xml";
            return Path.Combine(Path.GetDirectoryName(assemblyPathAll), fileName);
        }
        private void SetUpdateRadioButton()
        {
            switch (updateCheckButtonName)
            {
                case "radioButton_EachTenMinutes":
                    radioButton_EachTenMinutes.IsChecked = true;
                    break;
                case "radioButton_EachHour":
                    radioButton_EachHour.IsChecked = true;
                    break;
                default:
                    radioButton_OnLoad.IsChecked = true;
                    break;
            }
        }
        private void StartUpdateTimer()
        {
            timer = new Forms.Timer();
            if (updateCheckButtonName != null)
            {
                switch (updateCheckButtonName)
                {
                    case "radioButton_EachTenMinutes":
                        timer.Interval = 10 * 60 * 1000; // 10 minutes
                        break;
                    case "radioButton_EachHour":
                        timer.Interval = 60 * 60 * 1000; // 1 hour
                        break;
                }
                timer.Tick += Timer_Tick;
                timer.Start();
            }
        }
        private void notifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }
        private void ShowMainWindow()
        {
            this.Topmost = true;
            this.Left = SystemParameters.WorkArea.Right - this.Width;
            this.Top = SystemParameters.WorkArea.Bottom - this.Height;
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }
        private void CitrusUpdaterWPF_Closed(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }
        private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            this.notifyIcon.Dispose();
            Application.Current.Shutdown();
        }
        private void RadioButtonUpdate_Checked(object sender, RoutedEventArgs e)
        {
            updateCheckButtonName = (this.groupBox_UpdateCheck.Content as Grid)
                .Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked.Value)?.Name;

            SaveSettings();
            RestartUpdateTimer();
        }
        private void SaveSettings()
        {
            string settingsFilePath = GetSettingsFilePath();

            if (File.Exists(settingsFilePath))
            {
                File.Delete(settingsFilePath);
            }

            using (FileStream fs = new FileStream(settingsFilePath, FileMode.Create))
            {
                XmlSerializer xSer = new XmlSerializer(typeof(string));
                xSer.Serialize(fs, updateCheckButtonName);
            }
        }
        private void LoadLastSuccessfulChange()
        {
            string lastChangeFilePath = GetLastChangeFilePath();
            if (File.Exists(lastChangeFilePath))
            {
                lastSuccessfulChange = File.ReadAllText(lastChangeFilePath);
                textBlock_LastChange.Text = $"Последнее изменение: {lastSuccessfulChange}";
            }
            else
            {
                lastSuccessfulChange = "Неизвестно";
                textBlock_LastChange.Text = "Последнее изменение: Неизвестно";
            }
        }
        private void LoadChangeHistory()
        {
            string changeHistoryFilePath = GetChangeHistoryFilePath();
            if (File.Exists(changeHistoryFilePath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(changeHistoryFilePath);
                XmlNodeList changeNodes = xmlDoc.SelectNodes("/changelog/change");

                textBox_Info.Clear();
                foreach (XmlNode changeNode in changeNodes)
                {
                    string number = changeNode.SelectSingleNode("number").InnerText;
                    string description = changeNode.SelectSingleNode("description").InnerText;

                    textBox_Info.AppendText($"Номер изменения: {number}{Environment.NewLine}");
                    textBox_Info.AppendText($"{description}{Environment.NewLine}{Environment.NewLine}");
                }

                // Отображаем номер первого изменения
                XmlNode firstChangeNode = xmlDoc.SelectSingleNode("/changelog/change[1]/number");
                if (firstChangeNode != null)
                {
                    textBlock_LastChange.Text = $"Последнее изменение: {firstChangeNode.InnerText}";
                    SaveLastSuccessfulChange(firstChangeNode.InnerText);
                }
            }
        }
        private string GetLastChangeFilePath()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = LastChangeFileName;
            return Path.Combine(Path.GetDirectoryName(assemblyPathAll), fileName);
        }
        private string GetChangeHistoryFilePath()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = ChangeHistoryFileName;
            return Path.Combine(Path.GetDirectoryName(assemblyPathAll), fileName);
        }
        private void SaveLastSuccessfulChange(string changeNumber)
        {
            string lastChangeFilePath = GetLastChangeFilePath();
            File.WriteAllText(lastChangeFilePath, changeNumber);
            lastSuccessfulChange = changeNumber;
            textBlock_LastChange.Text = $"Последнее изменение: {changeNumber}";
        }
        private void SaveChangeHistory(string changeNumber, string description)
        {
            string changeHistoryFilePath = GetChangeHistoryFilePath();
            XmlDocument xmlDoc = new XmlDocument();

            if (File.Exists(changeHistoryFilePath))
            {
                xmlDoc.Load(changeHistoryFilePath);
            }
            else
            {
                XmlDeclaration xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement root = xmlDoc.CreateElement("changelog");
                xmlDoc.AppendChild(root);
                xmlDoc.InsertBefore(xmlDeclaration, root);
            }

            XmlNode rootElement = xmlDoc.DocumentElement;
            XmlElement changeElement = xmlDoc.CreateElement("change");

            XmlElement numberElement = xmlDoc.CreateElement("number");
            numberElement.InnerText = changeNumber;
            changeElement.AppendChild(numberElement);

            XmlElement descriptionElement = xmlDoc.CreateElement("description");
            descriptionElement.InnerText = description;
            changeElement.AppendChild(descriptionElement);

            rootElement.AppendChild(changeElement);
            xmlDoc.Save(changeHistoryFilePath);
        }
        private void RestartUpdateTimer()
        {
            timer?.Stop();
            timer = new Forms.Timer();

            switch (updateCheckButtonName)
            {
                case "radioButton_EachTenMinutes":
                    timer.Interval = 10 * 60 * 1000;
                    break;
                case "radioButton_EachHour":
                    timer.Interval = 60 * 60 * 1000;
                    break;
                default:
                    return;
            }
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            if (!IsRevitRunning())
            {
                _ = CheckForUpdates();
            }
            else
            {
                ShowRevitRunningMessage();
            }
            RestartUpdateTimer();
        }
        private bool IsRevitRunning()
        {
            return Process.GetProcesses().Any(p => p.ProcessName.Equals("Revit", StringComparison.OrdinalIgnoreCase));
        }
        private void ShowRevitRunningMessage()
        {
            Dispatcher.Invoke(() => {
                textBox_Info.Clear();
                textBox_Info.AppendText("Закройте Revit перед проверкой обновлений!");
            });
        }
        private async void Button_CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRevitRunning())
            {
                await CheckForUpdates();
            }
            else
            {
                ShowRevitRunningMessage();
            }
        }
        private async Task CheckForUpdates()
        {
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;

            string addinsDataURLString = "http://citrusbim.com/addinsdata.xml";

            if (await IsUrlAvailable(addinsDataURLString))
            {
                await ProcessUpdates(addinsDataURLString);
            }
            else
            {
                ShowServerConnectionError();
            }

            progressBar.Visibility = Visibility.Hidden;
        }
        private async Task<bool> IsUrlAvailable(string url)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    ShowErrorMessage($"Ошибка доступа к серверу: {response.StatusCode}");
                    return false;
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                ShowErrorMessage($"Ошибка запроса: {httpRequestException.Message}");
                return false;
            }
            catch (TaskCanceledException taskCanceledException) when (!taskCanceledException.CancellationToken.IsCancellationRequested)
            {
                ShowErrorMessage($"Тайм-аут запроса: {taskCanceledException.Message}");
                return false;
            }
            catch (TimeoutException timeoutException)
            {
                ShowErrorMessage($"Превышен тайм-аут запроса: {timeoutException.Message}");
                return false;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Неизвестная ошибка: {ex.Message}");
                return false;
            }
        }
        private async Task ProcessUpdates(string addinsDataURLString)
        {
            try
            {
                XmlDocument addinsDataXML = new XmlDocument();
                using (HttpResponseMessage response = await httpClient.GetAsync(addinsDataURLString))
                {
                    response.EnsureSuccessStatusCode();
                    string xmlContent = await response.Content.ReadAsStringAsync();
                    addinsDataXML.LoadXml(xmlContent);
                }

                XmlNode yearsNode = addinsDataXML.SelectSingleNode("addinsinfo/years[@name='Yars']");
                List<string> yearsList = GetYearsList(yearsNode);
                XmlNode root = addinsDataXML.SelectSingleNode("addinsinfo/folder[@name='Addins']");
                XmlNode filesForDel = addinsDataXML.SelectSingleNode("addinsinfo/foldersfordel[@name='ForDel']");

                string username = GetUsername();

                progressBar.Maximum = yearsList.Count;
                foreach (string year in yearsList)
                {
                    if (!RunCreateFoldersVersions.ContainsKey(year) || !RunCreateFoldersVersions[year])
                    {
                        await Task.Run(() => CreateFolders(root, year, username));
                    }

                    if (!RunDownloadFilesVersions.ContainsKey(year) || !RunDownloadFilesVersions[year])
                    {
                        await Task.Run(() => DownloadFiles(root, year, username));
                    }
                    Dispatcher.Invoke(() => progressBar.Value++);
                }

                await Task.Run(() => DeleteFoldersAndFiles(filesForDel, username));

                await DisplayChangeLog();
            }
            catch (HttpRequestException httpRequestException)
            {
                ShowErrorMessage($"Ошибка при обработке обновлений: {httpRequestException.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Неизвестная ошибка при обработке обновлений: {ex.Message}");
            }
        }
        private string GetUsername()
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] pathComponents = userProfilePath.Split(Path.DirectorySeparatorChar);
            return pathComponents.Last();
        }
        private static List<string> GetYearsList(XmlNode yearsNode)
        {
            return yearsNode.ChildNodes
                            .Cast<XmlNode>()
                            .Select(child => child.Attributes["name"].Value)
                            .ToList();
        }
        private void CreateFolders(XmlNode node, string year, string username)
        {
            if (!RunCreateFoldersVersions.ContainsKey(year))
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
        private void DownloadFiles(XmlNode node, string year, string username)
        {
            if (!RunDownloadFilesVersions.ContainsKey(year))
            {
                RunDownloadFilesVersions[year] = true;
            }

            List<Task> downloadTasks = new List<Task>();

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

                        downloadTasks.Add(DownloadFileWithRetryAsync(downloadUrl, targetPath));
                    }
                    else if (child.Name == "folder")
                    {
                        string folderName = child.Attributes["name"].Value;
                        string targetPath = child.Attributes["target_path"].Value.Replace("Year", year).Replace("%username%", username);

                        if (!Directory.Exists(targetPath))
                        {
                            Directory.CreateDirectory(targetPath);
                        }
                        DownloadFiles(child, year, username);
                    }
                }
            }

            Task.WhenAll(downloadTasks).Wait();

            RunDownloadFilesVersions[year] = false;
        }
        private async Task DownloadFileWithRetryAsync(string downloadUrl, string targetPath, int retryCount = 3)
        {
            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                try
                {
                    byte[] data = await httpClient.GetByteArrayAsync(downloadUrl);
                    await WriteAllBytesAsync(targetPath, data);

                    // Verify file size
                    if (new FileInfo(targetPath).Length > 0)
                    {
                        break; // Exit loop if download is successful and file is not empty
                    }
                }
                catch (HttpRequestException httpRequestException)
                {
                    ShowErrorMessage($"Ошибка при загрузке файла (попытка {attempt + 1} из {retryCount}): {httpRequestException.Message}");
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Ошибка при загрузке файла (попытка {attempt + 1} из {retryCount}): {ex.Message}");
                }
            }
        }
        private async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await fs.WriteAsync(bytes, 0, bytes.Length);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => textBox_Info.AppendText($"Ошибка при записи файла {path}: {ex.Message}{Environment.NewLine}"));
            }
        }
        private void DeleteFoldersAndFiles(XmlNode node, string username)
        {
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name == "folder")
                    {
                        string targetPath = child.Attributes["target_path"].Value.Replace("%username%", username);
                        if (Directory.Exists(targetPath))
                        {
                            Directory.Delete(targetPath, true);
                        }
                    }
                    else if (child.Name == "file")
                    {
                        string targetPath = child.Attributes["target_path"].Value.Replace("%username%", username);
                        if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }
                    }
                }
            }
        }
        private void ShowServerConnectionError()
        {
            Dispatcher.Invoke(() => {
                textBox_Info.Clear();
                textBox_Info.AppendText("Отсутствует подключение к серверу!");
            });
        }
        private async Task DisplayChangeLog()
        {
            string changelogURLString = "http://citrusbim.com/changelog.xml";
            if (await IsUrlAvailable(changelogURLString))
            {
                XmlDocument xmlDoc = new XmlDocument();
                using (HttpResponseMessage response = await httpClient.GetAsync(changelogURLString))
                {
                    response.EnsureSuccessStatusCode();
                    string xmlContent = await response.Content.ReadAsStringAsync();
                    xmlDoc.LoadXml(xmlContent);
                }
                XmlNodeList changeList = xmlDoc.SelectNodes("/changelog/change");

                Dispatcher.Invoke(() => {
                    textBox_Info.Clear();

                    foreach (XmlNode changeNode in changeList)
                    {
                        string number = changeNode.SelectSingleNode("number").InnerText;
                        string description = changeNode.SelectSingleNode("description").InnerText;

                        textBox_Info.AppendText($"Номер изменения: {number}{Environment.NewLine}");
                        textBox_Info.AppendText($"{description}{Environment.NewLine}{Environment.NewLine}");

                        // Save the last successful change number
                        SaveChangeHistory(number, description);
                    }

                    // Save the first change number as the last successful change
                    XmlNode firstChangeNode = xmlDoc.SelectSingleNode("/changelog/change[1]/number");
                    if (firstChangeNode != null)
                    {
                        SaveLastSuccessfulChange(firstChangeNode.InnerText);
                    }
                });
            }
            else
            {
                ShowServerConnectionError();
            }
        }
        private void image_CitrusLogo_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.citrusbim.com/") { UseShellExecute = true });
        }
        private void ShowErrorMessage(string message)
        {
            Dispatcher.Invoke(() => {
                textBox_Info.AppendText($"{message}{Environment.NewLine}");
            });
        }
    }

}

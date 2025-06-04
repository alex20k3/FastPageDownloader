using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.IO;       
using System.Text.RegularExpressions; 
using AngleSharp.Html.Parser; 
using AngleSharp.Dom;         
using AngleSharp.Html.Dom;   

using System.Net.Mime;



namespace FastPageDownloader1
{
    public partial class Form1 : Form
    {
       
        private static readonly HttpClient httpClient = new HttpClient();

        public Form1()
        {
            InitializeComponent();
            
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            txtSaveFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DownloadedPages");
        }

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = txtSaveFolder.Text; 
                DialogResult result = fbd.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    txtSaveFolder.Text = fbd.SelectedPath;
                }
            }
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            string[] urls = txtUrls.Lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            string saveFolderPath = txtSaveFolder.Text;

            if (urls.Length == 0)
            {
                MessageBox.Show("Пожалуйста, введите хотя бы один URL.", "Нет URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(saveFolderPath))
            {
                MessageBox.Show("Пожалуйста, выберите папку для сохранения.", "Нет папки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(saveFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(saveFolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось создать папку: {ex.Message}", "Ошибка папки", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            btnDownload.Enabled = false;
            btnBrowseFolder.Enabled = false;
            txtUrls.Enabled = false;
            lstLog.Items.Clear();
            progressBar.Value = 0;
            progressBar.Maximum = urls.Length;
            lblStatus.Text = $"Скачано 0 из {urls.Length}";

            List<Task> downloadTasks = new List<Task>();
            int completedCount = 0;

            foreach (string urlString in urls)
            {
               
                string currentUrl = urlString;
                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        LogMessage($"Начало загрузки: {currentUrl}");
                        var downloadResult = await DownloadPageInternalAsync(currentUrl);

                        if (downloadResult.IsSuccess && downloadResult.Content != null)
                        {
                           
                            var parser = new HtmlParser();
                            IDocument document = await parser.ParseDocumentAsync(downloadResult.Content);

                            
                            LogMessage($"HTML для {currentUrl} получен. Начинаю внедрение CSS...");
                            document = await EmbedCssAsync(document, currentUrl);

                          
                            LogMessage($"CSS для {currentUrl} обработан. Начинаю внедрение изображений...");
                            document = await EmbedImagesAsync(document, currentUrl);

                           
                            string finalHtml;
                            var formatter = AngleSharp.Html.PrettyMarkupFormatter.Instance;
                            using (var stringWriter = new System.IO.StringWriter())
                            {
                                document.ToHtml(stringWriter, formatter);
                                finalHtml = stringWriter.ToString();
                            }

                            string fileName = GenerateFileNameFromUrl(currentUrl);
                            string filePath = Path.Combine(saveFolderPath, fileName);

                            using (var streamWriter = new StreamWriter(filePath, false, Encoding.UTF8))
                            {
                                await streamWriter.WriteAsync(finalHtml); 
                            }
                            LogMessage($"Сохранено (с CSS и изображениями): {currentUrl} -> {fileName}");
                        }
                        else
                        {
                            if (downloadResult.StatusCode.HasValue)
                            {
                                LogMessage($"Ошибка HTTP: {currentUrl} - {downloadResult.StatusCode} - {downloadResult.ErrorMessage}");
                            }
                            else
                            {
                                LogMessage($"Ошибка загрузки: {currentUrl} - {downloadResult.ErrorMessage}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                       
                        if (ex is AngleSharp.Dom.DomException domEx) 
                        {
                            LogMessage($"Ошибка AngleSharp DOM при обработке {currentUrl}: {domEx.Message} (Код ошибки: {domEx.Code})");
                        }
                        else if (ex is System.Xml.XmlException xmlEx)
                        {
                            LogMessage($"Ошибка XML при обработке {currentUrl}: {xmlEx.Message}");
                        }
                        else
                        {
                            LogMessage($"Непредвиденная ошибка при обработке URL {currentUrl}: {ex.Message}");
                        }
                    }
                    finally
                    {
                        this.Invoke((MethodInvoker)delegate {
                            completedCount++;
                            progressBar.Value = completedCount;
                            lblStatus.Text = $"Скачано {completedCount} из {urls.Length}";
                        });
                    }
                }));
            }

            await Task.WhenAll(downloadTasks);

            LogMessage("--- Скачивание завершено ---");
            MessageBox.Show("Скачивание завершено!", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);

            btnDownload.Enabled = true;
            btnBrowseFolder.Enabled = true;
            txtUrls.Enabled = true;
        }

        private async Task<string> DownloadPageAsync(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url; 
            }

            Uri uri = new Uri(url); 
            HttpResponseMessage response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode(); 
            return await response.Content.ReadAsStringAsync();
        }

        private string GenerateFileNameFromUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string path = uri.AbsolutePath;
                string fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(fileName) || fileName == "/")
                {
                    fileName = "index.html";
                }
                else
                {
                    if (!Path.HasExtension(fileName))
                    {
                        fileName += ".html";
                    }
                }

                string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                Regex r = new Regex(string.Format("[{0}]", Regex.Escape(invalidChars)));
                fileName = r.Replace(fileName, "_");

                if (fileName.Length > 100)
                {
                    string ext = Path.GetExtension(fileName); 
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    int maxNameLength = 100 - (ext?.Length ?? 0);
                    if (nameWithoutExt.Length > maxNameLength)
                    {
                        nameWithoutExt = nameWithoutExt.Substring(0, maxNameLength);
                    }
                    fileName = nameWithoutExt + ext;
                }
                return fileName;
            }
            catch (UriFormatException)
            {
                return Guid.NewGuid().ToString() + ".html";
            }
        }

        private async Task<(string Content, System.Net.HttpStatusCode? StatusCode, bool IsSuccess, string ErrorMessage)> DownloadPageInternalAsync(string url)
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }

            try
            {
                Uri uri = new Uri(url);
                HttpResponseMessage response = await httpClient.GetAsync(uri);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return (responseContent, response.StatusCode, true, null);
                }
                else
                {
                    string errorMessage = $"Статус: {response.StatusCode} ({response.ReasonPhrase}). Ответ сервера: {responseContent.Substring(0, Math.Min(responseContent.Length, 200))}";
                    return (null, response.StatusCode, false, errorMessage);
                }
            }
            catch (UriFormatException uriEx)
            {
                return (null, null, false, $"Неверный URL '{url}': {uriEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                return (null, null, false, $"Сетевая ошибка для '{url}': {httpEx.Message}");
            }
            catch (Exception ex)
            {
                return (null, null, false, $"Непредвиденная ошибка для '{url}': {ex.Message}");
            }
        }
        private async Task<IDocument> EmbedCssAsync(IDocument document, string baseUrl) 
        {
            
            if (document == null)
            {
                LogMessage($"Документ для {baseUrl} не предоставлен для внедрения CSS. Пропуск.");
                return null; 
            }

            try
            {
                var head = document.Head;
                if (head == null)
                {
                    LogMessage($"Не найден <head> на странице {baseUrl}, CSS не будет встроен.");
                    return document;
                }

                var cssLinks = document.QuerySelectorAll("link[rel='stylesheet'][href]")
                                       .OfType<IHtmlLinkElement>()
                                       .ToList();

                if (!cssLinks.Any())
                {
                   
                    return document; 
                }

                StringBuilder embeddedCssBuilder = new StringBuilder();
                Uri baseUri = new Uri(baseUrl);

                LogMessage($"Найдено {cssLinks.Count} CSS ссылок на странице {baseUrl} (EmbedCssAsync). Начинаю обработку...");

                foreach (var linkElement in cssLinks)
                {
                    string cssRelativeUrl = linkElement.Href;
                    if (string.IsNullOrWhiteSpace(cssRelativeUrl))
                    {
                        LogMessage($"Пустой href у CSS ссылки на {baseUrl} (EmbedCssAsync)");
                        continue;
                    }

                    Uri absoluteCssUri;
                    try
                    {
                        absoluteCssUri = new Uri(baseUri, cssRelativeUrl);
                    }
                    catch (UriFormatException ex)
                    {
                        LogMessage($"Некорректный CSS URL: '{cssRelativeUrl}' на странице {baseUrl} (EmbedCssAsync). Ошибка: {ex.Message}");
                        continue;
                    }

                    try
                    {
                        LogMessage($"Загрузка CSS: {absoluteCssUri} (EmbedCssAsync)");
                        HttpResponseMessage cssResponse = await httpClient.GetAsync(absoluteCssUri);

                        if (cssResponse.IsSuccessStatusCode)
                        {
                            string cssContent = await cssResponse.Content.ReadAsStringAsync();
                            embeddedCssBuilder.AppendLine($"/* CSS from {absoluteCssUri} */");
                            embeddedCssBuilder.AppendLine(cssContent);
                            embeddedCssBuilder.AppendLine();
                            linkElement.Remove();
                            LogMessage($"CSS {absoluteCssUri} успешно загружен и ссылка удалена (EmbedCssAsync).");
                        }
                        else
                        {
                            LogMessage($"Ошибка загрузки CSS {absoluteCssUri} (EmbedCssAsync): {cssResponse.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Критическая ошибка при загрузке или обработке CSS {absoluteCssUri} (EmbedCssAsync): {ex.Message}");
                    }
                }

                if (embeddedCssBuilder.Length > 0)
                {
                    var styleElement = document.CreateElement("style");
                    styleElement.SetAttribute("type", "text/css");
                    styleElement.TextContent = embeddedCssBuilder.ToString();
                    head.InsertBefore(styleElement, head.FirstChild);
                    LogMessage($"Все загруженные CSS встроены в <style> для {baseUrl} (EmbedCssAsync)");
                }

                return document; 
            }
            catch (Exception ex)
            {
                LogMessage($"Общая ошибка при обработке HTML для внедрения CSS ({baseUrl}, EmbedCssAsync): {ex.Message}");
                return document; 
            }
        }

        private async Task<IDocument> EmbedImagesAsync(IDocument document, string baseUrl)
        {
            if (document == null)
            {
                LogMessage($"Документ для {baseUrl} не предоставлен для EmbedImagesAsync. Пропуск.");
                return null;
            }

            LogMessage($"Начинаю поиск и встраивание изображений для {baseUrl} (EmbedImagesAsync)...");
            var imageElements = document.QuerySelectorAll("img[src]") 
                                        .OfType<IHtmlImageElement>()   
                                        .ToList();

            LogMessage($"[DEBUG EmbedImagesAsync] Найдено IHtmlImageElement: {imageElements.Count}");

            if (!imageElements.Any())
            {
                LogMessage($"Изображения <img> не найдены на {baseUrl} или не являются IHtmlImageElement (EmbedImagesAsync).");
                return document;
            }

            LogMessage($"Найдено {imageElements.Count} тегов <img> на {baseUrl} (EmbedImagesAsync). Начинаю обработку...");
            Uri baseUri = new Uri(baseUrl);

            foreach (var imgElement in imageElements)
            {
                string imageRelativeUrl = imgElement.Source;
                LogMessage($"[DEBUG EmbedImagesAsync] Обработка img.Source: '{imageRelativeUrl}'");

                if (string.IsNullOrWhiteSpace(imageRelativeUrl))
                {
                    LogMessage($"Пустой src у тега <img> на {baseUrl} (EmbedImagesAsync). Пропуск.");
                    continue;
                }

                if (imageRelativeUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    LogMessage($"Пропуск уже встроенного изображения (Data URI): {imageRelativeUrl.Substring(0, Math.Min(imageRelativeUrl.Length, 60))}... (EmbedImagesAsync)");
                    continue;
                }

                Uri absoluteImageUri;
                try
                {
                    absoluteImageUri = new Uri(baseUri, imageRelativeUrl);
                    LogMessage($"[DEBUG EmbedImagesAsync] Сформирован абсолютный URL: {absoluteImageUri}");
                }
                catch (UriFormatException ex)
                {
                    LogMessage($"Некорректный URL изображения: '{imageRelativeUrl}' на странице {baseUrl} (EmbedImagesAsync). Ошибка: {ex.Message}");
                    continue;
                }
                catch (ArgumentNullException ex) 
                {
                    LogMessage($"ArgumentNullException при создании Uri для изображения: '{imageRelativeUrl}' на {baseUrl} (EmbedImagesAsync). Ошибка: {ex.Message}");
                    continue;
                }


                try
                {
                    LogMessage($"Загрузка изображения: {absoluteImageUri} (EmbedImagesAsync)");
                    

                    HttpResponseMessage imageResponse = await httpClient.GetAsync(absoluteImageUri);
                    LogMessage($"[DEBUG EmbedImagesAsync] Ответ от сервера для {absoluteImageUri}: {imageResponse.StatusCode}");

                    if (imageResponse.IsSuccessStatusCode)
                    {
                        byte[] imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                        string contentType = imageResponse.Content.Headers.ContentType?.MediaType;

                        
                        if (string.IsNullOrWhiteSpace(contentType) && absoluteImageUri.Segments.Length > 0)
                        {
                            string ext = Path.GetExtension(absoluteImageUri.AbsolutePath).ToLowerInvariant();
                            switch (ext)
                            {
                                case ".jpg":
                                case ".jpeg":
                                    contentType = "image/jpeg";
                                    break;
                                case ".png":
                                    contentType = "image/png";
                                    break;
                                case ".gif":
                                    contentType = "image/gif";
                                    break;
                                case ".bmp":
                                    contentType = "image/bmp";
                                    break;
                                case ".webp":
                                    contentType = "image/webp";
                                    break;
                                default:
                                    contentType = "application/octet-stream"; 
                                    break;
                            }
                            LogMessage($"[DEBUG EmbedImagesAsync] ContentType не был предоставлен, угадан по расширению '{ext}': {contentType}");
                        }
                        else if (string.IsNullOrWhiteSpace(contentType))
                        {
                            contentType = "image/jpeg"; 
                            LogMessage($"[DEBUG EmbedImagesAsync] ContentType не был предоставлен и не удалось угадать, по умолчанию: {contentType}");
                        }


                        LogMessage($"[DEBUG EmbedImagesAsync] ContentType для {absoluteImageUri}: {contentType}, Размер: {imageBytes.Length} байт");

                        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                            contentType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase))
                        {
                            LogMessage($"Неподдерживаемый ContentType '{contentType}' для встраивания изображения {absoluteImageUri} (EmbedImagesAsync). Пропуск.");
                            continue;
                        }

                        if (imageBytes.Length == 0)
                        {
                            LogMessage($"Изображение {absoluteImageUri} пустое (0 байт). Пропуск встраивания.");
                            continue;
                        }

                        string base64Image = Convert.ToBase64String(imageBytes);
                        string dataUri = $"data:{contentType};base64,{base64Image}";

                        imgElement.Source = dataUri;
                        LogMessage($"Изображение {absoluteImageUri} успешно встроено как Data URI (EmbedImagesAsync).");
                    }
                    else
                    {
                        LogMessage($"Ошибка загрузки изображения {absoluteImageUri} (EmbedImagesAsync): {imageResponse.StatusCode}");
                        string errorContent = await imageResponse.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(errorContent))
                        {
                            LogMessage($"[DEBUG EmbedImagesAsync] Тело ошибки для {absoluteImageUri}: {errorContent.Substring(0, Math.Min(errorContent.Length, 200))}");
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    LogMessage($"HttpRequestException при загрузке изображения {absoluteImageUri} (EmbedImagesAsync): {httpEx.Message}. StatusCode: {httpEx.StatusCode}");
                }
                catch (TaskCanceledException ex) 
                {
                    LogMessage($"TaskCanceledException (возможно, таймаут) при загрузке изображения {absoluteImageUri} (EmbedImagesAsync): {ex.Message}");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"Критическая ошибка при загрузке или обработке изображения {absoluteImageUri} (EmbedImagesAsync): {ex.GetType().FullName} - {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n  Внутреннее исключение: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}";
                    }
                    
                    LogMessage(errorMessage);
                }
            }
            return document;
        }


        private void LogMessage(string message)
        {
            if (lstLog.InvokeRequired)
            {
                lstLog.Invoke((MethodInvoker)delegate {
                    lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - lstLog.ClientSize.Height / lstLog.ItemHeight);
                });
            }
            else
            {
                lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - lstLog.ClientSize.Height / lstLog.ItemHeight);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            txtUrls.Text = "https://example.com/\r\nhttps://www.google.com/\r\nhttps://nonexistentpage12345.com/";
        }
    }
}
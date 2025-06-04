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
                MessageBox.Show("����������, ������� ���� �� ���� URL.", "��� URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(saveFolderPath))
            {
                MessageBox.Show("����������, �������� ����� ��� ����������.", "��� �����", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    MessageBox.Show($"�� ������� ������� �����: {ex.Message}", "������ �����", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            btnDownload.Enabled = false;
            btnBrowseFolder.Enabled = false;
            txtUrls.Enabled = false;
            lstLog.Items.Clear();
            progressBar.Value = 0;
            progressBar.Maximum = urls.Length;
            lblStatus.Text = $"������� 0 �� {urls.Length}";

            List<Task> downloadTasks = new List<Task>();
            int completedCount = 0;

            foreach (string urlString in urls)
            {
               
                string currentUrl = urlString;
                downloadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        LogMessage($"������ ��������: {currentUrl}");
                        var downloadResult = await DownloadPageInternalAsync(currentUrl);

                        if (downloadResult.IsSuccess && downloadResult.Content != null)
                        {
                           
                            var parser = new HtmlParser();
                            IDocument document = await parser.ParseDocumentAsync(downloadResult.Content);

                            
                            LogMessage($"HTML ��� {currentUrl} �������. ������� ��������� CSS...");
                            document = await EmbedCssAsync(document, currentUrl);

                          
                            LogMessage($"CSS ��� {currentUrl} ���������. ������� ��������� �����������...");
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
                            LogMessage($"��������� (� CSS � �������������): {currentUrl} -> {fileName}");
                        }
                        else
                        {
                            if (downloadResult.StatusCode.HasValue)
                            {
                                LogMessage($"������ HTTP: {currentUrl} - {downloadResult.StatusCode} - {downloadResult.ErrorMessage}");
                            }
                            else
                            {
                                LogMessage($"������ ��������: {currentUrl} - {downloadResult.ErrorMessage}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                       
                        if (ex is AngleSharp.Dom.DomException domEx) 
                        {
                            LogMessage($"������ AngleSharp DOM ��� ��������� {currentUrl}: {domEx.Message} (��� ������: {domEx.Code})");
                        }
                        else if (ex is System.Xml.XmlException xmlEx)
                        {
                            LogMessage($"������ XML ��� ��������� {currentUrl}: {xmlEx.Message}");
                        }
                        else
                        {
                            LogMessage($"�������������� ������ ��� ��������� URL {currentUrl}: {ex.Message}");
                        }
                    }
                    finally
                    {
                        this.Invoke((MethodInvoker)delegate {
                            completedCount++;
                            progressBar.Value = completedCount;
                            lblStatus.Text = $"������� {completedCount} �� {urls.Length}";
                        });
                    }
                }));
            }

            await Task.WhenAll(downloadTasks);

            LogMessage("--- ���������� ��������� ---");
            MessageBox.Show("���������� ���������!", "������", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                    string errorMessage = $"������: {response.StatusCode} ({response.ReasonPhrase}). ����� �������: {responseContent.Substring(0, Math.Min(responseContent.Length, 200))}";
                    return (null, response.StatusCode, false, errorMessage);
                }
            }
            catch (UriFormatException uriEx)
            {
                return (null, null, false, $"�������� URL '{url}': {uriEx.Message}");
            }
            catch (HttpRequestException httpEx)
            {
                return (null, null, false, $"������� ������ ��� '{url}': {httpEx.Message}");
            }
            catch (Exception ex)
            {
                return (null, null, false, $"�������������� ������ ��� '{url}': {ex.Message}");
            }
        }
        private async Task<IDocument> EmbedCssAsync(IDocument document, string baseUrl) 
        {
            
            if (document == null)
            {
                LogMessage($"�������� ��� {baseUrl} �� ������������ ��� ��������� CSS. �������.");
                return null; 
            }

            try
            {
                var head = document.Head;
                if (head == null)
                {
                    LogMessage($"�� ������ <head> �� �������� {baseUrl}, CSS �� ����� �������.");
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

                LogMessage($"������� {cssLinks.Count} CSS ������ �� �������� {baseUrl} (EmbedCssAsync). ������� ���������...");

                foreach (var linkElement in cssLinks)
                {
                    string cssRelativeUrl = linkElement.Href;
                    if (string.IsNullOrWhiteSpace(cssRelativeUrl))
                    {
                        LogMessage($"������ href � CSS ������ �� {baseUrl} (EmbedCssAsync)");
                        continue;
                    }

                    Uri absoluteCssUri;
                    try
                    {
                        absoluteCssUri = new Uri(baseUri, cssRelativeUrl);
                    }
                    catch (UriFormatException ex)
                    {
                        LogMessage($"������������ CSS URL: '{cssRelativeUrl}' �� �������� {baseUrl} (EmbedCssAsync). ������: {ex.Message}");
                        continue;
                    }

                    try
                    {
                        LogMessage($"�������� CSS: {absoluteCssUri} (EmbedCssAsync)");
                        HttpResponseMessage cssResponse = await httpClient.GetAsync(absoluteCssUri);

                        if (cssResponse.IsSuccessStatusCode)
                        {
                            string cssContent = await cssResponse.Content.ReadAsStringAsync();
                            embeddedCssBuilder.AppendLine($"/* CSS from {absoluteCssUri} */");
                            embeddedCssBuilder.AppendLine(cssContent);
                            embeddedCssBuilder.AppendLine();
                            linkElement.Remove();
                            LogMessage($"CSS {absoluteCssUri} ������� �������� � ������ ������� (EmbedCssAsync).");
                        }
                        else
                        {
                            LogMessage($"������ �������� CSS {absoluteCssUri} (EmbedCssAsync): {cssResponse.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"����������� ������ ��� �������� ��� ��������� CSS {absoluteCssUri} (EmbedCssAsync): {ex.Message}");
                    }
                }

                if (embeddedCssBuilder.Length > 0)
                {
                    var styleElement = document.CreateElement("style");
                    styleElement.SetAttribute("type", "text/css");
                    styleElement.TextContent = embeddedCssBuilder.ToString();
                    head.InsertBefore(styleElement, head.FirstChild);
                    LogMessage($"��� ����������� CSS �������� � <style> ��� {baseUrl} (EmbedCssAsync)");
                }

                return document; 
            }
            catch (Exception ex)
            {
                LogMessage($"����� ������ ��� ��������� HTML ��� ��������� CSS ({baseUrl}, EmbedCssAsync): {ex.Message}");
                return document; 
            }
        }

        private async Task<IDocument> EmbedImagesAsync(IDocument document, string baseUrl)
        {
            if (document == null)
            {
                LogMessage($"�������� ��� {baseUrl} �� ������������ ��� EmbedImagesAsync. �������.");
                return null;
            }

            LogMessage($"������� ����� � ����������� ����������� ��� {baseUrl} (EmbedImagesAsync)...");
            var imageElements = document.QuerySelectorAll("img[src]") 
                                        .OfType<IHtmlImageElement>()   
                                        .ToList();

            LogMessage($"[DEBUG EmbedImagesAsync] ������� IHtmlImageElement: {imageElements.Count}");

            if (!imageElements.Any())
            {
                LogMessage($"����������� <img> �� ������� �� {baseUrl} ��� �� �������� IHtmlImageElement (EmbedImagesAsync).");
                return document;
            }

            LogMessage($"������� {imageElements.Count} ����� <img> �� {baseUrl} (EmbedImagesAsync). ������� ���������...");
            Uri baseUri = new Uri(baseUrl);

            foreach (var imgElement in imageElements)
            {
                string imageRelativeUrl = imgElement.Source;
                LogMessage($"[DEBUG EmbedImagesAsync] ��������� img.Source: '{imageRelativeUrl}'");

                if (string.IsNullOrWhiteSpace(imageRelativeUrl))
                {
                    LogMessage($"������ src � ���� <img> �� {baseUrl} (EmbedImagesAsync). �������.");
                    continue;
                }

                if (imageRelativeUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    LogMessage($"������� ��� ����������� ����������� (Data URI): {imageRelativeUrl.Substring(0, Math.Min(imageRelativeUrl.Length, 60))}... (EmbedImagesAsync)");
                    continue;
                }

                Uri absoluteImageUri;
                try
                {
                    absoluteImageUri = new Uri(baseUri, imageRelativeUrl);
                    LogMessage($"[DEBUG EmbedImagesAsync] ����������� ���������� URL: {absoluteImageUri}");
                }
                catch (UriFormatException ex)
                {
                    LogMessage($"������������ URL �����������: '{imageRelativeUrl}' �� �������� {baseUrl} (EmbedImagesAsync). ������: {ex.Message}");
                    continue;
                }
                catch (ArgumentNullException ex) 
                {
                    LogMessage($"ArgumentNullException ��� �������� Uri ��� �����������: '{imageRelativeUrl}' �� {baseUrl} (EmbedImagesAsync). ������: {ex.Message}");
                    continue;
                }


                try
                {
                    LogMessage($"�������� �����������: {absoluteImageUri} (EmbedImagesAsync)");
                    

                    HttpResponseMessage imageResponse = await httpClient.GetAsync(absoluteImageUri);
                    LogMessage($"[DEBUG EmbedImagesAsync] ����� �� ������� ��� {absoluteImageUri}: {imageResponse.StatusCode}");

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
                            LogMessage($"[DEBUG EmbedImagesAsync] ContentType �� ��� ������������, ������ �� ���������� '{ext}': {contentType}");
                        }
                        else if (string.IsNullOrWhiteSpace(contentType))
                        {
                            contentType = "image/jpeg"; 
                            LogMessage($"[DEBUG EmbedImagesAsync] ContentType �� ��� ������������ � �� ������� �������, �� ���������: {contentType}");
                        }


                        LogMessage($"[DEBUG EmbedImagesAsync] ContentType ��� {absoluteImageUri}: {contentType}, ������: {imageBytes.Length} ����");

                        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                            contentType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase))
                        {
                            LogMessage($"���������������� ContentType '{contentType}' ��� ����������� ����������� {absoluteImageUri} (EmbedImagesAsync). �������.");
                            continue;
                        }

                        if (imageBytes.Length == 0)
                        {
                            LogMessage($"����������� {absoluteImageUri} ������ (0 ����). ������� �����������.");
                            continue;
                        }

                        string base64Image = Convert.ToBase64String(imageBytes);
                        string dataUri = $"data:{contentType};base64,{base64Image}";

                        imgElement.Source = dataUri;
                        LogMessage($"����������� {absoluteImageUri} ������� �������� ��� Data URI (EmbedImagesAsync).");
                    }
                    else
                    {
                        LogMessage($"������ �������� ����������� {absoluteImageUri} (EmbedImagesAsync): {imageResponse.StatusCode}");
                        string errorContent = await imageResponse.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(errorContent))
                        {
                            LogMessage($"[DEBUG EmbedImagesAsync] ���� ������ ��� {absoluteImageUri}: {errorContent.Substring(0, Math.Min(errorContent.Length, 200))}");
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    LogMessage($"HttpRequestException ��� �������� ����������� {absoluteImageUri} (EmbedImagesAsync): {httpEx.Message}. StatusCode: {httpEx.StatusCode}");
                }
                catch (TaskCanceledException ex) 
                {
                    LogMessage($"TaskCanceledException (��������, �������) ��� �������� ����������� {absoluteImageUri} (EmbedImagesAsync): {ex.Message}");
                }
                catch (Exception ex)
                {
                    string errorMessage = $"����������� ������ ��� �������� ��� ��������� ����������� {absoluteImageUri} (EmbedImagesAsync): {ex.GetType().FullName} - {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n  ���������� ����������: {ex.InnerException.GetType().FullName} - {ex.InnerException.Message}";
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
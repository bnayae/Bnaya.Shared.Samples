#region Using

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Drawing;
using System.Diagnostics;
using System.Net.Http;

#endregion // Using

namespace TDF.Samples
{
    ////////////////////////////////////////////////////////
    //                                                    //
    //   garbage  <-------  downloader <--------------    //
    //                      /           \            |    //
    //                  writer         parser --------    //
    //                                                    //
    ////////////////////////////////////////////////////////
    class Program
    {
        #region Constants

        //private const string URL_CRAWL_TARGET = "http://blogs.microsoft.co.il/blogs/bnaya/";
        //private const string URL_CRAWL_TARGET = "http://delicious.com/bnaya";
        //private const string URL_CRAWL_TARGET = "https://www.google.com/search?q=bnaya+eshet&hl=en&tbm=isch&tbo=u&source=univ&sa=X&ei=p6iAUYCYBInHswb0rIGoAw&ved=0CEoQsAQ&biw=2560&bih=1199";
        //private const string URL_CRAWL_TARGET = "http://www.imdb.com/";
        private const string URL_CRAWL_TARGET = "http://www.zap.co.il/";

        //private const string LINK_REGEX = @"(https?)://[-A-Z0-9+&@#/%?=~_|!:,.;]*[^\""]*";
        private const string LINK_REGEX = @"(http)://[-A-Z0-9+&@#/%?=~_|!:,.;]*[^\""]*";
        private static readonly string[] IGNORE_LIST = { ".js", ".ashx", "-dtd", ".swf" };

        // Throttling
        private const int DOWNLOADER_MAX_MESSAGE_PER_TASK = 10; // fairness
        private const int DOWNLOADER_MAX_DEGREE_OF_PARALLELISM = 5;
        private const int DOWNLOADER_BOUNDED_CAPACITY = 1000; // the size of the block input buffer

        private const int MAX_MESSAGE_PER_TASK = 2; // fairness
        private const int WRITER_MAX_DEGREE_OF_PARALLELISM = 3;

        // timeout and cancellation
        private const int DOWNLOAD_TIMEOUT_SEC = 10;
        private const int COMPLETE_AFTER_SEC = 30;

        private static readonly Size MIN_SIZE = new Size(40, 40);

        private static readonly Regex _linkRegex = new Regex(LINK_REGEX);

        private static readonly ConcurrentDictionary<string, bool> _urls = new ConcurrentDictionary<string, bool>();
        private static readonly ConcurrentDictionary<string, bool> _images = new ConcurrentDictionary<string, bool>();

        private static readonly Predicate<HttpContentInfo> _isWebPage = content => IsMediaType(content, "text/html");
        private static readonly Predicate<HttpContentInfo> _isImage = content => IsMediaTypeStartWith(content, "image");

        #endregion // Constants

        static void Main(string[] args)
        {
            StartCrawling();
            Console.ReadKey();
        }

        static async void StartCrawling()
        {
            if (!Directory.Exists("Images"))
                Directory.CreateDirectory("Images");
            try
            {
                #region Dataflow block Options

                var downloaderOptions = new ExecutionDataflowBlockOptions
                {
                    // enforce fairness, after handling n messages 
                    // the block's task will be re-schedule.
                    // this will give the opportunity for other block 
                    // to actively process there messages (to avoid over subscription 
                    // the Tpl dataflow does not schedule all task at once if the machine
                    // does not have enough cores)
                    MaxMessagesPerTask = DOWNLOADER_MAX_MESSAGE_PER_TASK,
                    // by default Tpl dataflow assign a single task per block, 
                    // but you can control it by using the MaxDegreeOfParallelism
                    MaxDegreeOfParallelism = DOWNLOADER_MAX_DEGREE_OF_PARALLELISM,
                    // the size of the block input buffer
                    BoundedCapacity = DOWNLOADER_BOUNDED_CAPACITY
                };

                var transformerOptions = new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = MAX_MESSAGE_PER_TASK,
                };

                var writerOptions = new ExecutionDataflowBlockOptions
                {
                    // by default Tpl dataflow assign a single task per block, 
                    // but you can control it by using the MaxDegreeOfParallelism
                    MaxDegreeOfParallelism = WRITER_MAX_DEGREE_OF_PARALLELISM,
                    // MaxMessagesPerTask = MAX_MESSAGE_PER_TASK,
                };

                var linkOption = new DataflowLinkOptions { PropagateCompletion = true };

                #endregion // Dataflow block Options

                #region Downloader

                var downloader = new TransformBlock<string, HttpContentInfo>( // "text/html, image/jpeg"
                    async (url) =>
                    {
                        try
                        {
                            #region Validation

                            if (_urls.ContainsKey(url))
                                return null;
                            _urls.TryAdd(url, true);

                            if (!ShouldContinue(url))
                                return null;

                            #endregion // Validation

                            HttpClient client = new HttpClient();
                            client.Timeout = TimeSpan.FromSeconds(DOWNLOAD_TIMEOUT_SEC);

                            //Trace.WriteLine("Downloading: " + url);

                            // using IOCP the thread pool worker thread does return to the pool
                            HttpResponseMessage response = await client.GetAsync(url);
                            if (!response.IsSuccessStatusCode)
                            {
                                WriteToConsole("Fail to download html: [{0}] \r\n\tStatus Code = {1}", ConsoleColor.Red, url, response.StatusCode);
                                return null;
                            }
                            HttpContent content = response.Content;

                            var contentType = content.Headers.ContentType;
                            string mediaType = contentType.MediaType;

                            #region Validation

                            if (contentType == null)
                            {
                                WriteToConsole("Unknown content type [{0}]: {1}", ConsoleColor.Gray,
                                    mediaType, url);
                                return null;
                            }

                            #endregion // Validation
                            WriteToConsole("Downloaded [{0}]: {1}", ConsoleColor.White,
                               mediaType, url);

                            var info = new HttpContentInfo(url, response.Content);
                            if (!IsMediaType(info, "text/html"))
                                Trace.WriteLine("Downloaded [" + mediaType + "]: " + url);
                            return info;
                        }
                        #region Exception Handling

                        catch (UriFormatException ex)
                        {
                            WriteToConsole("invalid URL", ConsoleColor.Red, ex.Message);
                        }
                        catch (WebException ex)
                        {
                            WriteToConsole("Error: [{0}]\r\n\t{1}", ConsoleColor.Red, url, ex.Message);
                        }
                        catch (AggregateException ex)
                        {
                            foreach (var exc in ex.Flatten().InnerExceptions)
                            {
                                WriteToConsole("Error: [{0}]\r\n\t{1}", ConsoleColor.Red, url, exc.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteToConsole("Unexpected error: {0}", ConsoleColor.Red, ex.Message);
                        }

                        #endregion // Exception Handling

                        return null;
                    }, downloaderOptions);

                #endregion // Downloader

                #region Parser

                var parser = new TransformManyBlock<HttpContentInfo, string>(
                    async contentInfo =>
                    {
                        HttpContent content = contentInfo.Content;

                        // using IOCP the thread pool worker thread does return to the pool
                        string html = await content.ReadAsStringAsync();
                        var output = new List<string>();
                        try
                        {
                            var links = _linkRegex.Matches(html);
                            foreach (Match item in links)
                            {
                                var value = item.Value;
                                //Trace.WriteLine("\t\tPARSED: " + value);
                                output.Add(value);
                            }
                        }
                        #region Exception Handling

                        catch (Exception ex)
                        {
                            WriteToConsole("Error {0}", ConsoleColor.Red, ex.Message);
                        }

                        #endregion // Exception Handling

                        return output;
                    }, transformerOptions);

                #endregion // Parser

                #region Writer

                var writer = new ActionBlock<HttpContentInfo>(async contentInfo =>
                {
                    try
                    {
                        HttpContent content = contentInfo.Content;

                        // using IOCP the thread pool worker thread does return to the pool
                        using (Stream source = await content.ReadAsStreamAsync())
                        using (var image = Image.FromStream(source))
                        {
                            string fileName = Path.GetFileName(contentInfo.Url);

                            //Trace.WriteLine("\tWRITTING: " + contentInfo.Url);

                            #region Validation

                            if (!_images.TryAdd(fileName, true))
                                return;

                            if (image.Width < MIN_SIZE.Width || image.Height < MIN_SIZE.Height)
                                return;

                            #endregion // Validation

                            string name = @"Images\" + fileName;

                            using (Stream dest = OpenWriteAsync(name))
                            {
                                source.Position = 0;
                                // using IOCP the thread pool worker thread does return to the pool
                                await source.CopyToAsync(dest);
                                WriteToConsole("{0}: Width:{1}, Height:{2}", ConsoleColor.Yellow,
                                    fileName, image.Width, image.Height);
                            }
                        }
                    }
                    #region Exception Handling

                    catch (WebException ex)
                    {
                        WriteToConsole("Error: [{0}]\r\n\t{1}", ConsoleColor.Red, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        WriteToConsole("Unexpected error: {0}", ConsoleColor.Red, ex.Message);
                    }

                    #endregion // Exception Handling
                }, writerOptions);

                #endregion // Writer

                var garbageContent = DataflowBlock.NullTarget<HttpContentInfo>();
                var garbageUrl = DataflowBlock.NullTarget<string>();


                #region LinkTo

                ////////////////////////////////////////////////////////
                //                                                    //
                //   garbage  <-------  downloader <--------------    //
                //                      /           \            |    //
                //                  writer         parser --------    //
                //                                                    //
                ////////////////////////////////////////////////////////
                downloader.LinkTo(writer, linkOption, _isImage);
                downloader.LinkTo(parser, linkOption, info => info != null);
                downloader.LinkTo(garbageContent, linkOption); // fallback (otherwise empty messages will be stack in the block buffer and the block will never complete)
                parser.LinkTo(downloader, linkOption, url => !string.IsNullOrEmpty(url));
                parser.LinkTo(garbageUrl, linkOption);

                #endregion // LinkTo

                downloader.Post(URL_CRAWL_TARGET);

                Console.WriteLine("Crawling");
                Thread.Sleep(COMPLETE_AFTER_SEC * 1000);

                #region Complete

                downloader.Complete();

                #region WriteToConsole ("Try to Complete...")

                ConsoleColor color = ConsoleColor.Yellow;
                WriteToConsole(
    @"Try to Complete (items in the buffer = 
            downloader:         is completed = {0}, input={1} , output={2}
            writer:             is completed = {3}, input ={4}
            parser:             is completed = {5}, input={6} , output={7}", color,
                              downloader.Completion.IsCompleted, downloader.InputCount, downloader.OutputCount,
                              writer.Completion.IsCompleted, writer.InputCount,
                              parser.Completion.IsCompleted, parser.InputCount, parser.OutputCount);

                #endregion // WriteToConsole ("Try to Complete...")

                Task completeAll = Task.WhenAll(
                    downloader.Completion,
                    parser.Completion,
                    writer.Completion);

                await Task.Run(async () =>
                {
                    while (!completeAll.IsCompleted)
                    {
                        await Task.Delay(2000);

                        #region WriteToConsole (status)

                        color = color == ConsoleColor.Magenta ? ConsoleColor.White : ConsoleColor.Yellow;

                        WriteToConsole(
          @"Complete Status (items in the buffer = 
            downloader:         is completed = {0}, input={1} , output={2}
            writer:             is completed = {3}, input ={4}
            parser:         is completed = {5}, input={6} , output={7}", color,
                          downloader.Completion.IsCompleted, downloader.InputCount, downloader.OutputCount,
                          writer.Completion.IsCompleted, writer.InputCount,
                          parser.Completion.IsCompleted, parser.InputCount, parser.OutputCount);
                    }

                    #endregion // WriteToConsole (status)
                });

                WriteToConsole("Complete (items in the writer input buffer = {0})", ConsoleColor.Green, writer.InputCount);

                #endregion // Complete
            }
            catch (Exception ex)
            {
                WriteToConsole("EXCEPTION: {0}", ConsoleColor.DarkRed, ex);
            }
        }

        #region WriteToConsole

        private static void WriteToConsole(string format, ConsoleColor color, params object[] texts)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(format, texts);
            Console.ResetColor();
        }

        #endregion // WriteToConsole

        #region OpenWriteAsync

        private static FileStream OpenWriteAsync(string path)
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
        }

        #endregion // OpenWriteAsync

        #region IsMediaType

        private static bool IsMediaType(HttpContentInfo contextInfo, string compareType)
        {
            if (contextInfo == null)
                return false;
            HttpContent context = contextInfo.Content;
            if (context == null)
                return false;
            string mediaType = context.Headers.ContentType.MediaType;
            return string.Compare(mediaType, compareType, true) == 0;
        }

        #endregion // IsMediaType

        #region IsMediaTypeStartWith

        private static bool IsMediaTypeStartWith(HttpContentInfo contextInfo, string compareType)
        {
            if (contextInfo == null)
                return false;
            HttpContent context = contextInfo.Content;
            if (context == null)
                return false;
            string mediaType = context.Headers.ContentType.MediaType;
            bool result = mediaType.StartsWith(compareType, StringComparison.InvariantCultureIgnoreCase);
            return result;
        }

        #endregion // IsMediaTypeStartWith

        #region ShouldContinue

        private static bool ShouldContinue(string url)
        {
            foreach (var item in IGNORE_LIST)
            {
                if (url.EndsWith(item))
                    return false;
            }
            return true;
        }

        #endregion // ShouldContinue

        #region HttpContentInfo

        private class HttpContentInfo
        {
            public HttpContentInfo(string url, HttpContent content)
            {
                Content = content;
                Url = url;
            }

            public string Url { get; private set; }
            public HttpContent Content { get; private set; }
        }

        #endregion // HttpContentInfo
    }
}

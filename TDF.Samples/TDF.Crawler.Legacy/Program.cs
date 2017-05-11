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
using System.Xml.Linq;
using HtmlAgilityPack;

#endregion // Using

namespace TDF.Samples
{

    ///////////////////////////////////////////////////////////////////////
    // garbage  <-------  downloader <-------------------------           //
    //                       |                                |          //
    //               contentBroadcaster                       |          //
    //              /                  \                      |          //
    //      imgParser                   linkParsers--->linkBroadcaster   //
    //          |                                             |          //
    //       writer <-----------------------------------------            //
    ///////////////////////////////////////////////////////////////////////
    class Program
    {
        #region Constants

        //private const string URL_CRAWL_TARGET = "https://plus.google.com/photos/104141767384686008012/albums?banner=pwa&gpsrc=pwrd1#photos/104141767384686008012/albums/5645547115543828129";
        //private const string URL_CRAWL_TARGET = "http://blogs.microsoft.co.il/blogs/bnaya/";
        // private const string URL_CRAWL_TARGET = "http://blogs.microsoft.co.il/blogs/bnaya/archive/2013/01/06/mef-2-0-toc.aspx";
        //private const string URL_CRAWL_TARGET = "http://www.imdb.com/";
        //private const string URL_CRAWL_TARGET = "https://www.google.com/search?q=dog&oq=dog&aqs=chrome..69i57j0l5.1514j0j4&sourceid=chrome&espv=210&es_sm=122&ie=UTF-8";
        private const string URL_CRAWL_TARGET = "http://www.zap.co.il/";


        // Throttling
        private const int DOWNLOADER_MAX_MESSAGE_PER_TASK = 3; // fairness
        private const int DOWNLOADER_MAX_DEGREE_OF_PARALLELISM = 10;
        private const int DOWNLOADER_BOUNDED_CAPACITY = 30; // the size of the block input buffer

        private const int MAX_MESSAGE_PER_TASK = 2; // fairness
        private const int WRITER_MAX_DEGREE_OF_PARALLELISM = 10;

        // timeout and cancellation
        private const int DOWNLOAD_TIMEOUT_SEC = 20;
        private const int COMPLETE_AFTER_SEC = 10;

        private static readonly Size MIN_SIZE = new Size(50, 50);

        private static ConcurrentDictionary<string, bool> _urls = new ConcurrentDictionary<string, bool>();
        private static ConcurrentDictionary<string, bool> _images = new ConcurrentDictionary<string, bool>();

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
                    MaxDegreeOfParallelism = 2, //DOWNLOADER_MAX_DEGREE_OF_PARALLELISM,
                    // the size of the block input buffer
                    BoundedCapacity = DOWNLOADER_BOUNDED_CAPACITY
                };

                var transformerOptions = new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = MAX_MESSAGE_PER_TASK,
                };

                var writerOptions = new ExecutionDataflowBlockOptions
                {
                    MaxMessagesPerTask = WRITER_MAX_DEGREE_OF_PARALLELISM,
                    MaxDegreeOfParallelism = DOWNLOADER_MAX_DEGREE_OF_PARALLELISM
                };

                var linkOption = new DataflowLinkOptions { PropagateCompletion = true };

                #endregion // Dataflow block Options

                #region Downloader

                var downloader = new TransformBlock<string, XElement>(
                    async (url) =>
                    {
                        try
                        {
                            #region Validation

                            //if (_urls.ContainsKey(url))
                            //    return null;
                            if (!_urls.TryAdd(url, true))
                                return null;

                            #endregion // Validation

                            // using IOCP the thread pool worker thread does return to the pool
                            WebClient wc = new WebClient();
                            Task<string> download = wc.DownloadStringTaskAsync(url);
                            Task cancel = Task.Delay(DOWNLOAD_TIMEOUT_SEC * 1000);
                            Task any = await Task.WhenAny(download, cancel);

                            #region Timeout validation

                            if (any == cancel)
                            {
                                wc.CancelAsync();
                                WriteToConsole("Cancel: [{0}]", ConsoleColor.Gray, url);
                                return null;
                            }

                            #endregion // Timeout validation

                            string html = download.Result;
                            WriteToConsole("Downloaded: {0}", ConsoleColor.White, url);

                            XElement e = HtmlToXElement(html);

                            return e;
                        }
                        #region Exception Handling

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

                #region Link Parser

                var linkParser = new TransformManyBlock<XElement, string>(
                    (html) =>
                    {
                        #region Validation

                        if (html == null)
                            return Enumerable.Empty<string>();

                        #endregion // Validation

                        var linkes = from item in html.Descendants()
                                     where item.Name.LocalName == "a"
                                     let href = item.Attribute("href")
                                     where href != null
                                     select href.Value;

                        try
                        {
                            var result = linkes.ToArray();
                            return result;
                        }
                        #region Exception Handling

                        catch (Exception ex)
                        {
                            WriteToConsole("Unexpected error: {0}", ConsoleColor.Red, ex.Message);
                            return Enumerable.Empty<string>();
                        }

                        #endregion // Exception Handling
                    }, transformerOptions);

                #endregion // Link Parser

                #region Image Parser

                var imgParser = new TransformManyBlock<XElement, string>(
                    (html) =>
                    {

                        var images = from item in html.Descendants()
                                     where item.Name.LocalName == "img"
                                     let src = item.Attribute("src")
                                     where src != null
                                     select src.Value;
                        try
                        {
                            var result = images.ToArray();
                            return result;
                        }
                        #region Exception Handling

                        catch (Exception ex)
                        {
                            WriteToConsole("Unexpected error: {0}", ConsoleColor.Red, ex.Message);
                            return Enumerable.Empty<string>();
                        }

                        #endregion // Exception Handling
                    }, transformerOptions);

                #endregion // Image Parser

                #region Writer

                var writer = new ActionBlock<string>(async url =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(url))
                            return;

                        WebClient wc = new WebClient();
                        // using IOCP the thread pool worker thread does return to the pool
                        byte[] buffer = await wc.DownloadDataTaskAsync(url);
                        string fileName = Path.GetFileName(url);

                        #region Validation

                        if (!_images.TryAdd(fileName, true))
                            return;

                        #endregion // Validation

                        string name = @"Images\" + fileName;

                        using (var image = Image.FromStream(new MemoryStream(buffer)))
                        {
                            if (image.Width > MIN_SIZE.Width && image.Height > MIN_SIZE.Height)
                            {
                                using (Stream srm = OpenWriteAsync(name))
                                {

                                    await srm.WriteAsync(buffer, 0, buffer.Length);
                                    WriteToConsole("{0}: Width:{1}, Height:{2}", ConsoleColor.Yellow,
                                        fileName, image.Width, image.Height);

                                }
                            }
                        }
                    }
                    #region Exception Handling

                    catch (WebException ex)
                    {
                        WriteToConsole("Error: [{0}]\r\n\t{1}", ConsoleColor.Red, url, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        WriteToConsole("Unexpected error: {0}", ConsoleColor.Red, ex.Message);
                    }

                    #endregion // Exception Handling
                }, writerOptions);

                #endregion // Writer

                var garbageA = DataflowBlock.NullTarget<XElement>();
                var garbageB = DataflowBlock.NullTarget<string>();

                // use to broadcast the downloader output to the image and link parsers
                var contentBroadcaster = new BroadcastBlock<XElement>(s => s);
                var linkBroadcaster = new BroadcastBlock<string>(s => s);

                #region LinkTo

                ///////////////////////////////////////////////////////////////////////
                // garbage  <-------  downloader <-------------------------           //
                //                       |                                |          //
                //               contentBroadcaster                       |          //
                //              /                  \                      |          //
                //      imgParser                   linkParsers--->linkBroadcaster   //
                //          |                                             |          //
                //       writer <-----------------------------------------            //
                ///////////////////////////////////////////////////////////////////////

                downloader.LinkTo(contentBroadcaster, linkOption, html => html != null);
                downloader.LinkTo(garbageA, linkOption /*, html => html == null*/); // fallback (otherwise empty messages will be stack in the block buffer and the block will never complete)
                contentBroadcaster.LinkTo(imgParser, linkOption);
                contentBroadcaster.LinkTo(linkParser, linkOption);
                linkParser.LinkTo(linkBroadcaster, linkOption);

                StringComparison comparison = StringComparison.InvariantCultureIgnoreCase;
                Predicate<string> linkFilter = link => link.StartsWith("http://");

                Predicate<string> imgFilter = url =>
                        url.StartsWith("http://") &&
                        (url.EndsWith(".jpg", comparison) ||
                        url.EndsWith(".png", comparison) ||
                        url.EndsWith(".gif", comparison));
                Predicate<string> imgToGarbageFilter = url => !imgFilter(url);

                imgParser.LinkTo(writer, linkOption, imgFilter);
                imgParser.LinkTo(garbageB, imgToGarbageFilter);
                linkBroadcaster.LinkTo(writer, linkOption, imgFilter);
                linkBroadcaster.LinkTo(downloader, linkOption, linkFilter);
                //linkBroadcaster.LinkTo(garbage);

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
            linkParser:         is completed = {5}, input={6} , output={7}
            imgParser:          is completed = {8}, input={9} , output={10}
            linkBroadcaster:    is completed = {11},
            contentBroadcaster: is completed = {12}", color,
                              downloader.Completion.IsCompleted, downloader.InputCount, downloader.OutputCount,
                              writer.Completion.IsCompleted, writer.InputCount,
                              linkParser.Completion.IsCompleted, linkParser.InputCount, linkParser.OutputCount,
                              imgParser.Completion.IsCompleted, imgParser.InputCount, imgParser.OutputCount,
                              linkBroadcaster.Completion.IsCompleted,
                              contentBroadcaster.Completion.IsCompleted);

                #endregion // WriteToConsole ("Try to Complete...")

                Task completeAll = Task.WhenAll(
                    downloader.Completion,
                    linkParser.Completion,
                    imgParser.Completion,
                    contentBroadcaster.Completion,
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
            linkParser:         is completed = {5}, input={6} , output={7}
            imgParser:          is completed = {8}, input={9} , output={10}
            linkBroadcaster:    is completed = {11},
            contentBroadcaster: is completed = {12}
", color,
                          downloader.Completion.IsCompleted, downloader.InputCount, downloader.OutputCount,
                          writer.Completion.IsCompleted, writer.InputCount,
                          linkParser.Completion.IsCompleted, linkParser.InputCount, linkParser.OutputCount,
                          imgParser.Completion.IsCompleted, imgParser.InputCount, imgParser.OutputCount,
                          linkBroadcaster.Completion.IsCompleted,
                          contentBroadcaster.Completion.IsCompleted);
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

        #region HtmlToXElement

        public static XElement HtmlToXElement(string html)
        {
            if (html == null)
                throw new ArgumentNullException("html");

            HtmlDocument doc = new HtmlDocument();
            doc.OptionOutputAsXml = true;
            doc.LoadHtml(html);
            using (var srm = new MemoryStream())
            {
                doc.Save(srm);
                srm.Seek(0, SeekOrigin.Begin);
                XElement e = XElement.Load(srm);
                return e;
            }
        }

        #endregion // HtmlToXElement

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
            //string escaped = System.Web.HttpUtility.HtmlEncode(path);
            //char[] illegal = Path.GetInvalidFileNameChars();
            //var legalPath = new StringBuilder(path);
            //foreach (var c in illegal)
            //{
            //    legalPath.Replace(c, '_');
            //}

            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
        }

        #endregion // OpenWriteAsync
    }
}

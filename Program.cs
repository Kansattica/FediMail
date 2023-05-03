using CliWrap;
using CliWrap.Buffered;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MimeKit;
using System.Configuration;
using System.Text;

namespace FediMail
{
    internal class Program
    {
        private static readonly TimeSpan sleepTime = TimeSpan.FromSeconds(30);
        private static readonly string msyncPath = ConfigurationManager.AppSettings["msyncPath"];

        private static readonly Queue<MimeMessage> replies = new Queue<MimeMessage>();
        private static readonly Queue<string> tempFiles = new Queue<string>();
        static async Task Main(string[] args)
        {
            bool stayAlive = args.Contains("--daemon");

            Console.WriteLine("StayAlive mode is: " + stayAlive);

            var mailConfig = new MailConfig
            {
                EmailAddress = ConfigurationManager.AppSettings["emailAddress"],
                EmailPassword = ConfigurationManager.AppSettings["emailPassword"],
                ImapHost = ConfigurationManager.AppSettings["imapHost"],
                SmtpHost = ConfigurationManager.AppSettings["smtpHost"]
            };

            do
            {
                try
                {
                    await DoEmailCheck(mailConfig);
                    await SendReplies(mailConfig);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    CleanupTempFiles();
                }

                if (stayAlive)
                {
                    Console.WriteLine($"Sleeping for {sleepTime} before checking again.");
                    await Task.Delay(sleepTime);
                }
            } while (stayAlive);

        }

        private static void CleanupTempFiles()
        {
            while (tempFiles.TryDequeue(out var path))
            {
                Console.WriteLine("Deleting temporary file " + path);
                File.Delete(path);
            }
        }

        private static async Task DoEmailCheck(MailConfig mailConfig)
        {
            using (var client = new ImapClient())
            {
                Console.WriteLine("Logging in to " + mailConfig.EmailAddress);
                await client.ConnectAsync(mailConfig.ImapHost, 0, true);
                await client.AuthenticateAsync(mailConfig.EmailAddress, mailConfig.EmailPassword);

                Console.WriteLine("I'm in.");

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);

                Console.WriteLine("Total messages: {0}", inbox.Count);
                Console.WriteLine("Recent messages: {0}", inbox.Recent);

                try
                {
                    for (int i = 0; i < inbox.Count; i++)
                    {
                        var message = inbox.GetMessage(i);
                        var tempPath = WritePostFile(message);

                        Console.WriteLine("Wrote post file to " + tempPath);

                        // check this result
                        // reply with output
                        var result = await Cli.Wrap(msyncPath)
                            .WithArguments(new[] { "queue", "post", tempPath })
                            .ExecuteBufferedAsync();

                        Console.WriteLine(result.StandardOutput);

                        await inbox.StoreAsync(i, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true });

                        replies.Enqueue(MakeReply(mailConfig, message, result, tempPath));
                    }

                    var syncResult = await Cli.Wrap(msyncPath)
                        .WithArguments(new[] { "sync", "--send-only" })
                        .ExecuteBufferedAsync();

                    Console.WriteLine(syncResult.StandardOutput);
                }
                finally
                {
                    try
                    {
                        inbox.Expunge();
                    }
                    finally
                    {
                        client.Disconnect(true);
                    }
                }
            }
        }

        private static MimeMessage MakeReply(MailConfig mailConfig, MimeMessage receivedMessage, BufferedCommandResult result, string tempFile)
        {
            var replyMessage = new MimeMessage();
            replyMessage.From.Add(new MailboxAddress("FediMail", mailConfig.EmailAddress));
            replyMessage.To.AddRange(receivedMessage.From);
            replyMessage.Subject = "Re: " + receivedMessage.Subject;
            var textBody = new TextPart("plain") { Text = $"Message sent. Output from msync: \n\n {result.StandardOutput}" };
            var attachText = new MimePart("text", "plain")
            {
                Content = new MimeContent(File.OpenRead(tempFile), ContentEncoding.Default),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = Path.GetFileName(tempFile) + ".txt"
            };

            replyMessage.Body = new Multipart("mixed")
            {
                textBody,
                attachText
            };

            return replyMessage;
        }

        private static async Task SendReplies(MailConfig config)
        {
            if (replies.Count == 0)
                return;

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(config.SmtpHost, 0, true);
                await client.AuthenticateAsync(config.EmailAddress, config.EmailPassword);

                try
                {
                    while (replies.TryDequeue(out var reply))
                    {
                        Console.WriteLine($"Sending {reply.Subject} to {string.Join(", ", reply.To.Select(x => x.Name))})");

                        await client.SendAsync(reply);
                    }
                }
                finally
                {
                    client.Disconnect(true);
                }
            }
        }

        private static UTF8Encoding utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static string WritePostFile(MimeMessage message)
        {
            string tempFile = MakeTempFile();

            var toWrite = new List<string>
            {
                "visibility=default",
                "--- post body below this line ---",
                message.GetTextBody(MimeKit.Text.TextFormat.Plain)
            };

            toWrite.InsertRange(0, HandleAttachments(message));

            if (!string.IsNullOrWhiteSpace(message.Subject))
                toWrite.Insert(0, $"cw={message.Subject}");

            File.WriteAllText(tempFile, string.Join(Environment.NewLine, toWrite), utf8NoBom);

            return tempFile;
        }

        private static string MakeTempFile(string withExtension = "")
        {
            var tempFile = Path.GetTempFileName();
            var fileWithExtension = tempFile + withExtension;
            if (tempFile != fileWithExtension)
            {
                File.Move(tempFile, fileWithExtension);
            }
            tempFiles.Enqueue(fileWithExtension);
            return fileWithExtension;
        }

        private static IEnumerable<string> HandleAttachments(MimeMessage message)
        {
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MessagePart)
                {
                    var fileName = attachment.ContentDisposition?.FileName;
                    Console.WriteLine("Not doing anything with attached message " + fileName);
                    //var rfc822 = (MessagePart)attachment;

                    //if (string.IsNullOrEmpty(fileName))
                    //    fileName = "attached-message.eml";

                    //using (var stream = File.Create(fileName))
                    //    rfc822.Message.WriteTo(stream);
                }
                else
                {
                    var part = (MimePart)attachment;
                    var fileName = part.FileName;


                    var tempFile = MakeTempFile(Path.GetExtension(fileName));

                    Console.WriteLine($"Got an attachment called {fileName}. Saving it to {tempFile}.");

                    using (var stream = File.OpenWrite(tempFile))
                        part.Content.DecodeTo(stream);

                    yield return "attach=" + tempFile;
                    yield return "description=" + fileName;
                }
            }
        }

    }
}
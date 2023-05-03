using CliWrap;
using CliWrap.Buffered;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System.Configuration;
using System.Text;

namespace FediMail
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            bool stayAlive = args.Contains("--daemon");
            var mailConfig = new MailConfig
            {
                EmailAddress = ConfigurationManager.AppSettings["emailAddress"],
                EmailPassword = ConfigurationManager.AppSettings["emailPassword"],
                ImapHost = ConfigurationManager.AppSettings["imapHost"],
                SmtpHost = ConfigurationManager.AppSettings["smtpHost"]
            };

            var msyncPath = ConfigurationManager.AppSettings["msyncPath"];

            do
            {
                await DoEmailCheck(mailConfig, msyncPath);
                if (stayAlive)
                    await Task.Delay(TimeSpan.FromSeconds(30));
            } while (stayAlive);

        }

        private static async Task DoEmailCheck(MailConfig mailConfig, string? msyncPath)
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

                        File.Delete(tempPath);

                        await inbox.StoreAsync(i, new StoreFlagsRequest(StoreAction.Add, MessageFlags.Deleted) { Silent = true });
                    }
                }
                finally
                {
                    inbox.Expunge();
                    client.Disconnect(true);
                }
            }
        }

        private static UTF8Encoding utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static string WritePostFile(MimeMessage message)
        {
            var tempFile = Path.GetTempFileName();

            var toWrite = new List<string>
            {
                "visibility=default",
                $"reply_id={tempFile}",
                "--- post body below this line ---",
                message.GetTextBody(MimeKit.Text.TextFormat.Plain)
            };

            if (!string.IsNullOrWhiteSpace(message.Subject))
                toWrite.Insert(0, $"cw={message.Subject}");

            File.WriteAllText(tempFile, string.Join(Environment.NewLine, toWrite), utf8NoBom);

            return tempFile;
        }

    }
}
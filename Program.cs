using CliWrap;
using CliWrap.Buffered;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System.Configuration;

namespace FediMail
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var mailConfig = new MailConfig
            {
                EmailAddress = ConfigurationManager.AppSettings["emailAddress"],
                EmailPassword = ConfigurationManager.AppSettings["emailPassword"],
                ImapHost = ConfigurationManager.AppSettings["imapHost"],
                SmtpHost = ConfigurationManager.AppSettings["smtpHost"]
            };

            var msyncPath = ConfigurationManager.AppSettings["msyncPath"];

            using (var client = new ImapClient())
            {
                Console.WriteLine("Logging in to " + mailConfig.EmailAddress);
                await client.ConnectAsync(mailConfig.ImapHost, 0, true);
                await client.AuthenticateAsync(mailConfig.EmailAddress, mailConfig.EmailPassword);

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
                            .WithArguments(new[] { "queue", tempPath })
                            .ExecuteBufferedAsync();

                        Console.WriteLine(result.StandardOutput);

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

        private static string WritePostFile(MimeMessage message)
        {
            var tempFile = Path.GetTempFileName();

            var toWrite = new List<string>
            {
                "visibility=default",
                "--- post body below this line ---",
                message.GetTextBody(MimeKit.Text.TextFormat.Plain)
            };

            if (!string.IsNullOrWhiteSpace(message.Subject))
                toWrite.Insert(0, $"cw={message.Subject}");

            File.WriteAllLines(tempFile, toWrite, System.Text.Encoding.UTF8);

            return tempFile;
        }

    }
}
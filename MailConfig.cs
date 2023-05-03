using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FediMail
{
    internal class MailConfig
    {
        public string EmailAddress { get; private set; }
        public string EmailPassword { get; private set; }
        public string ImapHost { get; private set; }
        public string SmtpHost { get; private set; }

        public MailConfig(string? emailAddress, string? emailPassword, string? imapHost, string? smtpHost)
        {
            EmailAddress = emailAddress ?? throw new NullReferenceException(nameof(EmailAddress));
            EmailPassword = emailPassword ?? throw new NullReferenceException(nameof(EmailPassword));
            ImapHost = imapHost ?? throw new NullReferenceException(nameof(ImapHost));
            SmtpHost = smtpHost ?? throw new NullReferenceException(nameof(SmtpHost));
        }
    }
}

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
        public string EmailAddress { get; set; }
        public string EmailPassword { get; set; }
        public string ImapHost { get; set; }
        public string SmtpHost { get; set; }
    }
}

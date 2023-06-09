# FediMail
## *It's Like Email*

FediMail is a C# program that aims to let you use that original decentralized federated social network, email, to post on the fediverse! It uses [msync](https://github.com/Kansattica/msync) to do that, because I wrote it and I like it and I think it has some useful features for this.

It's pretty bare-bones right now, but I'm working on it.

The email subject become the post's content warning. The email body will be the body. Emails with no subject will be posted CW-less. FediMail will always use your default post privacy. Email attachments will be attached to the post, and the file name will be used as the description. 

If you do run this, be aware that:

- FediMail will read and delete any email it turns into a post. You should really set up an email account just for it.
- FediMail will turn around and post from the default msync account. I plan on adding multiaccount support in the future.
- Any email sent to the inbox FediMail has access to will be turned into a post.
- FediMail only works with text email. HTML not guaranteed.

Things I would like to implement:

- "advanced mode" (email body is handed directly off to msync)
- multi-user support (check multiple inboxes, post to corresponding accounts)
- reply with notifications? this one's dangerous
- security (verify sent-from email, maybe GPG signatures?)

Please see below for a helpful diagram and rationale for this project.

![8510af3b4d1ff6f7](https://user-images.githubusercontent.com/10965841/235862049-1fe6749d-cdb2-47d8-8877-7d3defc14305.png)

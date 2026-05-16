using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ProjectTallify.Models;

namespace ProjectTallify.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailConfirmationAsync(User user, string confirmationLink)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = "Verify your Tallify organizer account",
                IsBodyHtml = true
            };

            message.To.Add(user.Email);

            var displayName = string.IsNullOrWhiteSpace(user.FirstName)
                ? "Organizer"
                : (user.FirstName + " " + (user.LastName ?? "")).Trim();

            message.Body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; padding: 40px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                    .header {{ text-align: center; margin-bottom: 30px; }}
                    .header h1 {{ color: #333; font-size: 24px; margin: 0; }}
                    .content {{ font-size: 16px; color: #555; line-height: 1.6; }}
                    .btn-container {{ text-align: center; margin-top: 30px; }}
                    .btn {{ display: inline-block; background-color: #007bff; color: #ffffff; padding: 12px 30px; font-size: 16px; font-weight: bold; text-decoration: none; border-radius: 5px; transition: background-color 0.3s; }}
                    .btn:hover {{ background-color: #0056b3; }}
                    .footer {{ margin-top: 40px; text-align: center; font-size: 12px; color: #aaa; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Verify Your Email</h1>
                    </div>
                    <div class=""content"">
                        <p>Hello <strong>{displayName}</strong>,</p>
                        <p>Welcome to Tallify! Please verify your email address to activate your organizer account.</p>
                        
                        <div class=""btn-container"">
                            <a href=""{confirmationLink}"" class=""btn"" style=""color: #ffffff;"">Verify Email</a>
                        </div>

                        <p style=""margin-top: 20px; text-align: center; font-size: 14px;"">
                            If the button doesn't work, use this URL:<br>
                            <a href=""{confirmationLink}"" style=""color: #007bff;"">{confirmationLink}</a>
                        </p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; {DateTime.Now.Year} Tallify. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            await client.SendMailAsync(message);
        }

        public async Task SendPasswordResetAsync(User user, string resetLink)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = "Reset your Tallify password",
                IsBodyHtml = true
            };

            message.To.Add(user.Email);

            var displayName = string.IsNullOrWhiteSpace(user.FirstName)
        ? "Organizer"
        : (user.FirstName + " " + (user.LastName ?? "")).Trim();

        message.Body = $@"
    <h2>Password Reset</h2>
    <p>Hello {displayName},</p>
    <p>Someone (hopefully you) requested a password reset for your Tallify account.</p>
    <p>Click the link below to set a new password:</p>
    <p><a href=""{resetLink}"">Reset my password</a></p>
    <p>If you did not request this, you can ignore this email.</p>
    <p>If the button doesn’t work, use this URL:</p>
    <p>{resetLink}</p>
    ";
            await client.SendMailAsync(message);
        }

        public async Task SendJudgeInvitationAsync(Judge judge, string inviteLink, string eventName, string eventCode)
        {
            if (string.IsNullOrWhiteSpace(judge.Email)) return;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = $"Invitation to Judge: {eventName}",
                IsBodyHtml = true
            };

            message.To.Add(judge.Email);

            // Professional HTML Template
            message.Body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; padding: 40px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                    .header {{ text-align: center; margin-bottom: 30px; }}
                    .header h1 {{ color: #333; font-size: 24px; margin: 0; }}
                    .content {{ font-size: 16px; color: #555; line-height: 1.6; }}
                    .credentials-box {{ background-color: #f9f9f9; border: 1px solid #ddd; border-radius: 6px; padding: 20px; margin: 20px 0; text-align: center; }}
                    .credential-item {{ margin: 10px 0; }}
                    .credential-label {{ font-size: 14px; color: #888; text-transform: uppercase; letter-spacing: 1px; display: block; margin-bottom: 5px; }}
                    .credential-value {{ font-size: 24px; font-weight: bold; color: #333; font-family: monospace; letter-spacing: 2px; }}
                    .btn-container {{ text-align: center; margin-top: 30px; }}
                    .btn {{ display: inline-block; background-color: #007bff; color: #ffffff; padding: 12px 30px; font-size: 16px; font-weight: bold; text-decoration: none; border-radius: 5px; transition: background-color 0.3s; }}
                    .btn:hover {{ background-color: #0056b3; }}
                    .footer {{ margin-top: 40px; text-align: center; font-size: 12px; color: #aaa; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>You're Invited to Judge!</h1>
                    </div>
                    <div class=""content"">
                        <p>Hello <strong>{judge.Name}</strong>,</p>
                        <p>You have been selected to be a judge for the event: <strong>{eventName}</strong>.</p>
                        <p>Please use the credentials below to log in and access the scoring dashboard.</p>

                        <div class=""credentials-box"">
                            <div class=""credential-item"">
                                <span class=""credential-label"">Event Access Code</span>
                                <span class=""credential-value"">{eventCode}</span>
                            </div>
                            <div class=""credential-item"">
                                <span class=""credential-label"">Your Personal PIN</span>
                                <span class=""credential-value"">{judge.Pin}</span>
                            </div>
                        </div>

                        <div class=""btn-container"">
                            <a href=""{inviteLink}"" class=""btn"" style=""color: #ffffff;"">Access Event</a>
                        </div>
                        
                        <p style=""text-align: center; margin-top: 15px;"">
                            When you click the button, you will be redirected to the event access page.
                        </p>

                        <p style=""margin-top: 20px; text-align: center; font-size: 14px;"">
                            Or verify directly via this link:<br>
                            <a href=""{inviteLink}"" style=""color: #007bff;"">{inviteLink}</a>
                        </p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; {DateTime.Now.Year} Tallify. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            await client.SendMailAsync(message);
        }

        public async Task SendJudgeVerificationLinkAsync(Judge judge, string verificationLink, string eventName)
        {
            if (string.IsNullOrWhiteSpace(judge.Email)) return;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = $"Verify your email for: {eventName}",
                IsBodyHtml = true
            };

            message.To.Add(judge.Email);

            message.Body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; padding: 40px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                    .header {{ text-align: center; margin-bottom: 30px; }}
                    .header h1 {{ color: #333; font-size: 24px; margin: 0; }}
                    .content {{ font-size: 16px; color: #555; line-height: 1.6; }}
                    .btn-container {{ text-align: center; margin-top: 30px; }}
                    .btn {{ display: inline-block; background-color: #007bff; color: #ffffff; padding: 12px 30px; font-size: 16px; font-weight: bold; text-decoration: none; border-radius: 5px; transition: background-color 0.3s; }}
                    .btn:hover {{ background-color: #0056b3; }}
                    .footer {{ margin-top: 40px; text-align: center; font-size: 12px; color: #aaa; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Verify Your Email</h1>
                    </div>
                    <div class=""content"">
                        <p>Hello <strong>{judge.Name}</strong>,</p>
                        <p>Welcome to Tallify! Please verify your email address to activate your judge account for <strong>{eventName}</strong>.</p>
                        
                        <div class=""btn-container"">
                            <a href=""{verificationLink}"" class=""btn"" style=""color: #ffffff;"">Verify Email</a>
                        </div>

                        <p style=""margin-top: 20px; text-align: center; font-size: 14px;"">
                            If the button doesn't work, use this URL:<br>
                            <a href=""{verificationLink}"" style=""color: #007bff;"">{verificationLink}</a>
                        </p>
                    </div>
                    <div class=""footer"">
                        <p>&copy; {DateTime.Now.Year} Tallify. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            await client.SendMailAsync(message);
        }
    }
}

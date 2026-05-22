using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ProjectTallify.Models;
using System;

namespace ProjectTallify.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public SmtpEmailSender(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailConfirmationAsync(Organizer organizer, string confirmationLink)
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

            message.To.Add(organizer.Email);

            var displayName = string.IsNullOrWhiteSpace(organizer.FirstName)
                ? "Organizer"
                : (organizer.FirstName + " " + (organizer.LastName ?? "")).Trim();

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

        public async Task SendPasswordResetAsync(Organizer organizer, string resetLink)
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

            message.To.Add(organizer.Email);

            var displayName = string.IsNullOrWhiteSpace(organizer.FirstName)
                ? "Organizer"
                : (organizer.FirstName + " " + (organizer.LastName ?? "")).Trim();

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

            // Theme color logic (fallback to Tallify primary if null)
            var themeColor = string.IsNullOrWhiteSpace(judge.Event.ThemeColor) ? "#007bff" : judge.Event.ThemeColor;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = $"Access Credentials: {eventName}",
                IsBodyHtml = true
            };

            message.To.Add(judge.Email);

            message.Body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; color: #1e293b; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; padding: 40px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }}
                    .header {{ text-align: center; margin-bottom: 25px; }}
                    .header h1 {{ color: #0f172a; font-size: 24px; margin: 0; font-weight: 800; }}
                    .content {{ font-size: 16px; line-height: 1.6; color: #475569; }}
                    .credential-box {{ background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; margin: 24px 0; overflow: hidden; border-left: 5px solid {themeColor}; }}
                    .cred-col {{ padding: 20px 10px; text-align: center; }}
                    .cred-label {{ font-size: 11px; text-transform: uppercase; color: #64748b; letter-spacing: 1px; font-weight: 800; display: block; margin-bottom: 8px; }}
                    .cred-value {{ font-size: 20px; font-weight: 800; color: {themeColor}; font-family: monospace; background: #f8fafc; padding: 6px 12px; border-radius: 6px; display: inline-block; letter-spacing: 1px; }}
                    .btn-container {{ text-align: center; margin-top: 30px; }}
                    .btn {{ display: inline-block; background-color: {themeColor}; color: #ffffff !important; padding: 14px 35px; font-size: 16px; font-weight: bold; text-decoration: none; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
                    .footer {{ margin-top: 40px; text-align: center; font-size: 11px; color: #94a3b8; border-top: 1px solid #f1f5f9; padding-top: 15px; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Your Panel Access is Ready</h1>
                    </div>
                    <div class=""content"">
                        <p>Hello <strong>{judge.Name}</strong>,</p>
                        <p>Your email has been verified. You may now access the scoring interface for <strong>{eventName}</strong> using the credentials below.</p>
                        
                        <div class=""credential-box"">
                            <table style=""width: 100%; border-collapse: collapse; table-layout: fixed;"">
                                <tr>
                                    <td class=""cred-col"" style=""width: 50%; border-right: 1px solid #e2e8f0;"">
                                        <span class=""cred-label"">Access Code</span>
                                        <div class=""cred-value"">{eventCode}</div>
                                    </td>
                                    <td class=""cred-col"" style=""width: 50%;"">
                                        <span class=""cred-label"">PIN</span>
                                        <div class=""cred-value"">{judge.Pin}</div>
                                    </td>
                                </tr>
                            </table>
                        </div>

                        <div class=""btn-container"">
                            <a href=""{inviteLink}"" class=""btn"">Join Event with Code</a>
                        </div>
                    </div>
                    <div class=""footer"">
                        <p>This is a secure automated message from Tallify.<br>Please do not share your PIN with anyone.</p>
                        <p>&copy; {DateTime.Now.Year} Tallify. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            await client.SendMailAsync(message);
        }

        public async Task SendJudgeVerificationLinkAsync(Judge judge, string verificationLink, Event ev)
        {
            if (string.IsNullOrWhiteSpace(judge.Email)) return;

            // Theme color logic (fallback to Tallify primary if null)
            var themeColor = string.IsNullOrWhiteSpace(ev.ThemeColor) ? "#007bff" : ev.ThemeColor;

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, _settings.FromName),
                Subject = $"Official Invitation to Judge: {ev.Name}",
                IsBodyHtml = true
            };

            message.To.Add(judge.Email);

            message.Body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; color: #1e293b; }}
                    .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; padding: 40px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }}
                    .header {{ text-align: center; margin-bottom: 30px; }}
                    .header h1 {{ color: #0f172a; font-size: 26px; margin: 0; font-weight: 800; }}
                    .content {{ font-size: 16px; line-height: 1.7; color: #475569; }}
                    .event-banner {{ background: #f1f5f9; padding: 24px; border-radius: 8px; margin: 24px 0; border-left: 5px solid {themeColor}; }}
                    .detail-row {{ margin-bottom: 8px; display: flex; }}
                    .detail-label {{ font-size: 11px; text-transform: uppercase; color: #64748b; letter-spacing: 0.5px; font-weight: 700; width: 80px; flex-shrink: 0; }}
                    .detail-value {{ font-size: 15px; font-weight: 600; color: #0f172a; }}
                    .btn-container {{ text-align: center; margin-top: 35px; }}
                    .btn {{ display: inline-block; background-color: {themeColor}; color: #ffffff !important; padding: 14px 35px; font-size: 16px; font-weight: bold; text-decoration: none; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }}
                    .footer {{ margin-top: 45px; text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; padding-top: 20px; }}
                </style>
            </head>
            <body>
                <div class=""container"">
                    <div class=""header"">
                        <h1>Welcome to the Panel!</h1>
                    </div>
                    <div class=""content"">
                        <p>Dear <strong>{judge.Name}</strong>,</p>
                        <p>We are delighted to inform you that you have been officially selected as a judge for an upcoming competition hosted on <strong>Tallify</strong>.</p>
                        
                        <div class=""event-banner"">
                            <div style=""font-size: 13px; font-weight: 800; color: {themeColor}; margin-bottom: 12px; text-transform: uppercase;"">Event Invitation Details</div>
                            
                            <div class=""detail-row"">
                                <span class=""detail-label"">Event:</span>
                                <span class=""detail-value"">{ev.Name}</span>
                            </div>
                            <div class=""detail-row"">
                                <span class=""detail-label"">Venue:</span>
                                <span class=""detail-value"">Divine Word College of Calapan — {ev.Venue}</span>
                            </div>

                            <div class=""detail-row"">
                                <span class=""detail-label"">Schedule:</span>
                                <span class=""detail-value"">{ev.Schedule:MMMM dd, yyyy} at {ev.Schedule:h:mm tt}</span>
                            </div>
                        </div>

                        <p>Your expertise is vital to the success of this event. To get started, we need to verify your email address to ensure you receive your secure scoring credentials on the day of the competition.</p>
                        
                        <div class=""btn-container"">
                            <a href=""{verificationLink}"" class=""btn"">Verify your Email</a>
                        </div>

                        <p style=""margin-top: 25px; font-size: 14px; text-align: center; color: #64748b;"">
                            After verification, you will receive a follow-up email containing your unique Access Code and PIN to access the judge screen.
                        </p>
                    </div>
                    <div class=""footer"">
                        <p>This is an automated invitation from the Tallify Competition System.</p>
                        <p>&copy; {DateTime.Now.Year} Tallify. All rights reserved.</p>
                    </div>
                </div>
            </body>
            </html>";

            await client.SendMailAsync(message);
        }
    }
}

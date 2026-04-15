using System;
using System.Net;
using System.Net.Mail;

namespace SLICE_Website.Services
{
    public class EmailService
    {
        private readonly string _systemEmail = "slice.automated@gmail.com";
        private readonly string _appPassword = "sbwzycmywldszfof";

        public bool SendPasswordResetEmail(string targetEmail, string resetCode)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(_systemEmail, _appPassword),
                    EnableSsl = true,
                };

                // Get the current year dynamically for the footer copyright
                string currentYear = DateTime.Now.Year.ToString();

                // Custom HTML Email Template for S.L.I.C.E.
                string emailBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                </head>
                <body style='margin: 0; padding: 0; background-color: #FDFBF7; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif;'>
                    
                    <table width='100%' cellpadding='0' cellspacing='0' border='0' style='background-color: #FDFBF7; padding: 40px 15px;'>
                        <tr>
                            <td align='center'>
                                <table width='100%' max-width='550' cellpadding='0' cellspacing='0' border='0' style='max-width: 550px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 8px 20px rgba(0,0,0,0.08);'>
                                    
                                    <tr>
                                        <td align='center' style='background-color: #2C3E50; padding: 30px 20px; border-bottom: 4px solid #E67E22;'>
                                            <h1 style='color: #ffffff; margin: 0; font-size: 26px; font-weight: 900; letter-spacing: 2px;'>
                                                S.L.I.C.E. <span style='color: #E67E22;'>ENTERPRISE</span>
                                            </h1>
                                        </td>
                                    </tr>

                                    <tr>
                                        <td style='padding: 40px 35px; text-align: left;'>
                                            <h2 style='color: #2C3E50; margin: 0 0 15px 0; font-size: 22px; font-weight: 800;'>Account Recovery</h2>
                                            
                                            <p style='color: #5D6D7E; font-size: 15px; line-height: 1.6; margin: 0 0 15px 0;'>Hello,</p>
                                            
                                            <p style='color: #5D6D7E; font-size: 15px; line-height: 1.6; margin: 0 0 25px 0;'>
                                                We received a request to bypass your credentials and reset your password for the S.L.I.C.E. POS System. Please use the secure verification code below to authorize this change:
                                            </p>

                                            <div style='background-color: #F8F9FA; border: 2px dashed #BDC3C7; border-radius: 8px; padding: 25px; text-align: center; margin: 30px 0;'>
                                                <span style='display: block; font-size: 42px; font-weight: 900; color: #E74C3C; letter-spacing: 10px; margin-left: 10px;'>
                                                    {resetCode}
                                                </span>
                                            </div>

                                            <p style='color: #5D6D7E; font-size: 14px; line-height: 1.6; margin: 0 0 15px 0;'>
                                                <strong style='color: #2C3E50;'>Security Notice:</strong> This code is single-use and will automatically expire in <strong style='color: #E74C3C;'>15 minutes</strong>. Do not share this code with anyone, including managers.
                                            </p>
                                            
                                            <p style='color: #95A5A6; font-size: 13px; line-height: 1.5; margin: 25px 0 0 0; font-style: italic;'>
                                                If you did not request a password reset, your account is safe. You can securely ignore and delete this email.
                                            </p>
                                        </td>
                                    </tr>

                                    <tr>
                                        <td align='center' style='background-color: #F0F3F4; padding: 20px 30px;'>
                                            <p style='color: #95A5A6; font-size: 12px; margin: 0 0 5px 0; font-weight: 600;'>
                                                This is an automated system broadcast. Please do not reply.
                                            </p>
                                            <p style='color: #BDC3C7; font-size: 11px; margin: 0;'>
                                                &copy; {currentYear} S.L.I.C.E. Enterprise Systems. All rights reserved.
                                            </p>
                                        </td>
                                    </tr>
                                    
                                </table>
                            </td>
                        </tr>
                    </table>

                </body>
                </html>";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_systemEmail, "S.L.I.C.E. System Security"),
                    Subject = $"Verification Code: {resetCode} - S.L.I.C.E. Account Recovery",
                    Body = emailBody,
                    IsBodyHtml = true, // This tells Gmail to render the HTML
                };

                mailMessage.To.Add(targetEmail);

                smtpClient.Send(mailMessage);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
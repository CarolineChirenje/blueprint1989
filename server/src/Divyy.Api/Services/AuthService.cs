using Divvy.Api.Data;
using Divvy.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace Divvy.Api.Services
{
    public class AuthService
    {
    private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly MfaService _mfaService;
        private readonly IPasswordHashingService _passwordHashingService;
        private readonly AppConfigService _appConfig;
        private readonly IEmailService _emailService;
        private readonly EmailTemplateService _emailTemplates;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            ApplicationDbContext context, 
            IConfiguration configuration, 
            MfaService mfaService, 
            IPasswordHashingService passwordHashingService,
            AppConfigService appConfig,
            IEmailService emailService,
            EmailTemplateService emailTemplates,
            ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _mfaService = mfaService;
            _passwordHashingService = passwordHashingService;
            _appConfig = appConfig;
            _emailService = emailService;
            _emailTemplates = emailTemplates;
            _logger = logger;
        }

        /// <summary>
        /// Enable MFA for a user and return setup details
        /// </summary>
        public async Task<(string? secret, string? qrCode, List<string> backupCodes)> EnableMfaAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (null, null, new List<string>());
            }

            var secret = _mfaService.GenerateMfaSecret();
            var backupCodes = _mfaService.GenerateBackupCodes();
            var qrCodeUrl = _mfaService.GenerateQrCodeUrl(user.Email, secret);

            // Don't save yet - user needs to confirm they can use an authenticator
            return (secret, qrCodeUrl, backupCodes);
        }

        /// <summary>
        /// Confirm MFA setup after user verifies the code
        /// </summary>
        public async Task<(bool success, string? error)> ConfirmMfaAsync(int userId, string secret, string verificationCode, List<string> backupCodes)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            // Verify the provided code works with the secret
            if (!_mfaService.VerifyTotpCode(secret, verificationCode))
            {
                return (false, "Invalid verification code");
            }

            user.IsMfaEnabled = true;
            user.MfaSecret = secret;
            user.MfaEnabledAt = DateTime.UtcNow;
            user.BackupCodes = backupCodes;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        /// <summary>
        /// Disable MFA for a user
        /// </summary>
        public async Task<(bool success, string? error)> DisableMfaAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            if (!user.IsMfaEnabled)
            {
                return (false, "MFA is not enabled for this user");
            }

            user.IsMfaEnabled = false;
            user.MfaSecret = null;
            user.MfaEnabledAt = null;
            user.BackupCodes = new List<string>();

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(User? user, string? token, DateTime? tokenExpiresAt, bool requiresMfa, bool passwordExpired, bool emailUnverified)> LoginByEmailAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower() && u.IsActive);
           
            if (user == null || !_passwordHashingService.VerifyPassword(password, user.PasswordHash))
            {
                return (null, null, null, false, false, false);
            }

            // Block login until email is verified
            if (!user.IsEmailVerified)
            {
                return (user, null, null, false, false, true);
            }

            // Check if password has expired
            var passwordExpirationDays = await _appConfig.GetIntAsync(AppConfigKeys.PasswordExpirationDays, 30);
            var passwordExpired = false;
            
            if (user.PasswordLastChanged.HasValue)
            {
                var daysSinceChange = (DateTime.UtcNow - user.PasswordLastChanged.Value).TotalDays;
                passwordExpired = daysSinceChange >= passwordExpirationDays;
            }
            else
            {
                // If PasswordLastChanged is null, consider it expired (force initial change)
                passwordExpired = true;
            }

            var (token, expiresAt) = GenerateJwtToken(user);
            return (user, token, expiresAt, user.IsMfaEnabled, passwordExpired, false);
        }

        public async Task<(User? user, string? error)> RegisterAsync(string email, string password, Role role = Role.Member)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail))
            {
                return (null, "Email already exists");
            }

            var user = new User
            {
                Email = normalizedEmail,
                PasswordHash = _passwordHashingService.HashPassword(password),
                PasswordLastChanged = DateTime.UtcNow,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsEmailVerified = true  // Admin-registered users skip email verification
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Seed all notification preferences as enabled for the new user
            var regTypeIds = await _context.NotificationTypes.Select(t => t.Id).ToListAsync();
            foreach (var typeId in regTypeIds)
            {
                _context.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId             = user.Id,
                    NotificationTypeId = typeId,
                    IsEnabled          = true
                });
            }
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            user.PasswordHash = ""; // Don't return the hash

            return (user, null);
        }

        /// <summary>
        /// Get all users for admin management
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// Get a single user by ID without loading the full table.
        /// </summary>
        public async Task<User?> GetUserByIdAsync(int id)
            => await _context.Users.FindAsync(id);

        /// <summary>
        /// Update user active status
        /// </summary>
        public async Task<(bool success, string? error)> UpdateUserStatusAsync(int userId, bool isActive)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (true, null);
        }

        /// <summary>
        /// Delete a user
        /// </summary>
        public async Task<(bool success, string? error)> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return (true, null);
        }

        /// <summary>
        /// Update user details including role
        /// </summary>
        public async Task<(bool success, string? error)> UpdateUserAsync(int userId, string email, string? firstName, string? lastName, Role role)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            // Check if email is taken by another user
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.Id != userId);
            if (existingUser != null)
            {
                return (false, "Email is already taken");
            }

            user.Email = email;
            user.FirstName = firstName ?? user.FirstName;
            user.LastName = lastName ?? user.LastName;
            user.Role = role;
            user.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();

            return (true, null);
        }

        public (string token, DateTime expiresAt) GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKeyString = Environment.GetEnvironmentVariable("JWT_KEY")
                              ?? _configuration["Jwt:Key"]
                              ?? "your-super-secret-jwt-key-change-this-must-be-32-chars";
            var key = Encoding.UTF8.GetBytes(jwtKeyString);

            // Read expiration from AppConfig (DB-driven), fall back to 120 minutes
            var expirationMinutes = _appConfig.GetIntAsync(AppConfigKeys.JwtExpirationMinutes, 120).GetAwaiter().GetResult();
            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("id", user.Id.ToString()),
                    new System.Security.Claims.Claim("email", user.Email),
                    new System.Security.Claims.Claim("role", user.Role.ToString()),
                    new System.Security.Claims.Claim("roleId", ((int)user.Role).ToString()),
                }),
                Expires = expiresAt,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return (tokenHandler.WriteToken(token), expiresAt);
        }
        
        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Users
                .AnyAsync(b => b.Email.ToLower() == email.ToLower());
        }
        
        public async Task<bool> NameExistsAsync(string firstName, string lastName)
        {
            return await _context.Users
                .AnyAsync(b => b.FirstName.ToLower() == firstName.ToLower() && b.LastName.ToLower() == lastName.ToLower());
        }
        
        public async Task<(bool success, string? token, DateTime? tokenExpiresAt, string? error)> VerifyMfaAsync(int userId, string code)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null)
            {
                return (false, null, null, "User not found");
            }

            if (!user.IsMfaEnabled || string.IsNullOrEmpty(user.MfaSecret))
            {
                return (false, null, null, "MFA is not enabled for this user");
            }

            // Block MFA completion until email is verified
            if (!user.IsEmailVerified)
            {
                return (false, null, null, "email_unverified");
            }

            // Validate TOTP code
            var isValid = _mfaService.VerifyTotpCode(user.MfaSecret, code);

            if (!isValid)
            {
                return (false, null, null, "Invalid MFA code");
            }

            // If valid → generate JWT
            var (token, expiresAt) = GenerateJwtToken(user);

            return (true, token, expiresAt, null);
        }

        public async Task<(User? user, string? error)> SignupAsync(DTOs.Auth.SignupRequest request)
        {
            // SuperAdmin cannot self-register
            if (request.Role == Role.SuperAdmin)
                return (null, "SuperAdmin role cannot be self-registered.");

            var email = request.Email.Trim().ToLowerInvariant();

            if (request.Role == Role.Admin)
            {
                var expectedPin = await _appConfig.GetStringAsync(AppConfigKeys.AdminSignupPin);
                if (string.IsNullOrWhiteSpace(expectedPin) || request.AdminPin != expectedPin)
                    return (null, "Invalid administration PIN. Please contact your system administrator.");
            }

            var user = new User
            {
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PasswordHash = _passwordHashingService.HashPassword(request.Password),
                PasswordLastChanged = DateTime.UtcNow,
                Role = request.Role,
                IsActive = true,
                IsMfaEnabled = false,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Seed all notification preferences as enabled for the new user
            var signupTypeIds = await _context.NotificationTypes.Select(t => t.Id).ToListAsync();
            foreach (var typeId in signupTypeIds)
            {
                _context.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId             = user.Id,
                    NotificationTypeId = typeId,
                    IsEnabled          = true
                });
            }
            await _context.SaveChangesAsync();

            // Send email verification — non-blocking, errors are logged only
            try
            {
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email for new user: {UserId}", user.Id);
            }

            return (user, null);
        }


        public async Task SkipMfaSetupAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            user.IsMfaEnabled = false;
            user.MfaSecret = null;
            user.MfaEnabledAt = null;
            user.BackupCodes = new List<string>();
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<(bool success, string? error)> UpdateProfileAsync(int userId, string firstName, string lastName, string? currentPassword, string? newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found");
            }

            // Update name fields
            user.FirstName = firstName.Trim();
            user.LastName = lastName.Trim();

            // If changing password, verify current password
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (string.IsNullOrEmpty(currentPassword))
                {
                    return (false, "Current password is required to set a new password");
                }

                if (!_passwordHashingService.VerifyPassword(currentPassword, user.PasswordHash))
                {
                    return (false, "Current password is incorrect");
                }

                user.PasswordHash = _passwordHashingService.HashPassword(newPassword);
                user.PasswordLastChanged = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool success, string? error, User? user, string? token, DateTime? tokenExpiresAt)> ChangeExpiredPasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
            {
                return (false, "User not found", null, null, null);
            }

            // Verify current password
            if (!_passwordHashingService.VerifyPassword(currentPassword, user.PasswordHash))
            {
                return (false, "Current password is incorrect", null, null, null);
            }

            // Update password and set last changed date
            user.PasswordHash = _passwordHashingService.HashPassword(newPassword);
            user.PasswordLastChanged = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Generate new token for the user to continue
            var (token, expiresAt) = GenerateJwtToken(user);
            return (true, null, user, token, expiresAt);
        }

        /// <summary>
        /// Initiates a forgot password flow by sending a reset link to the user's email.
        /// Returns success even if email not found (don't leak user existence).
        /// Enforces rate limiting: max requests per email per hour.
        /// </summary>
        /// <summary>
        /// Deterministic SHA-256 hash for reset tokens.
        /// PBKDF2 (used by HashPassword) produces a different hash every call due to a random salt,
        /// making it impossible to look up stored tokens. Reset tokens are already 32 random bytes,
        /// so salting is unnecessary — SHA-256 gives sufficient one-way security.
        /// </summary>
        private static string HashResetToken(string plainToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>SHA-256 hash for email verification tokens (same rationale as HashResetToken).</summary>
        private static string HashVerificationToken(string plainToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        /// <summary>
        /// Generates an email verification token, persists it, and sends the verification email.
        /// Returns false with a reason string when rate-limited; does not throw.
        /// </summary>
        public async Task<(bool sent, string? reason)> SendVerificationEmailAsync(User user)
        {
            // Rate-limiting: max N resends per hour per user
            var resendLimit = await _appConfig.GetIntAsync(AppConfigKeys.EmailVerificationResendLimitPerHour, 3);
            var oneHourAgo  = DateTime.UtcNow.AddHours(-1);
            // Only count tokens from successful sends (IsUsed=false AND CreatedAt recent)
            // We persist the token only after a successful send, so the count is accurate.
            var recentCount = await _context.EmailVerificationTokens
                .Where(t => t.UserId == user.Id && t.CreatedAt > oneHourAgo && !t.IsUsed)
                .CountAsync();

            if (recentCount >= resendLimit)
            {
                _logger.LogWarning("Email verification resend rate limit exceeded for user: {UserId}", user.Id);
                return (false, "rate_limited");
            }

            // Build the verification URL before touching the DB
            var domain = await _appConfig.GetStringAsync(AppConfigKeys.AppDomain, "");
            var path   = await _appConfig.GetStringAsync(AppConfigKeys.EmailVerificationUrlPath, "");

            // Generate URL-safe token
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            var plainToken = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            var hashedToken = HashVerificationToken(plainToken);

            var validityHours = await _appConfig.GetIntAsync(AppConfigKeys.EmailVerificationTokenValidityHours, 24);
            var expiresAt     = DateTime.UtcNow.AddHours(validityHours);

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(path))
            {
                _logger.LogWarning(
                    "AppDomain and/or EmailVerificationUrlPath not configured — verification email not sent for user {UserId}. " +
                    "To manually verify in development, use token: {PlainToken}",
                    user.Id, plainToken);
                return (false, "not_configured");
            }

            var verificationUrl = $"{domain.TrimEnd('/')}{path}".Replace("{token}", plainToken);
            var appName         = await _appConfig.GetStringAsync(AppConfigKeys.AppName, "App");

            var (html, plain) = await _emailTemplates.BuildVerificationEmailAsync(user.FirstName, verificationUrl, validityHours);
            var sent = await _emailService.SendEmailAsync(
                user.Email,
                $"{appName}: Verify your email address",
                html,
                plain);

            if (!sent)
            {
                _logger.LogWarning(
                    "SMTP send failed for user {UserId} ({Email}). " +
                    "Check SMTP settings in AppConfig. Verification URL (dev use only): {VerificationUrl}",
                    user.Id, user.Email, verificationUrl);
                return (false, "smtp_failed");
            }

            // Only persist the token after a successful send so the rate limit
            // counter accurately reflects emails the user actually received.
            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId    = user.Id,
                Token     = hashedToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsUsed    = false
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Verification email sent for user {UserId}", user.Id);
            return (sent, null);
        }

        /// <summary>
        /// Validates a plain verification token, marks it used, and sets IsEmailVerified on the user.
        /// </summary>
        public async Task<(bool success, string? error)> VerifyEmailAsync(string plainToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plainToken))
                    return (false, "Token is required");

                var hashedToken = HashVerificationToken(plainToken);

                var token = await _context.EmailVerificationTokens
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.Token == hashedToken);

                if (token == null)
                {
                    _logger.LogWarning("Email verification token not found");
                    return (false, "Invalid or expired verification link");
                }

                if (token.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Email verification token expired. TokenId: {TokenId}", token.Id);
                    return (false, "This verification link has expired. Please request a new one.");
                }

                if (token.IsUsed)
                {
                    _logger.LogWarning("Email verification token already used. TokenId: {TokenId}", token.Id);
                    return (false, "This verification link has already been used.");
                }

                if (token.User == null)
                    return (false, "User not found");

                token.User.IsEmailVerified = true;
                token.User.UpdatedAt       = DateTime.UtcNow;
                token.IsUsed               = true;
                token.UsedAt               = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Email verified for user {UserId}", token.UserId);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email token");
                return (false, "An error occurred while verifying your email");
            }
        }

        public async Task<(bool success, string message)> ForgotPasswordAsync(string email)
        {
            const string genericMessage = "If an account exists with this email, a password reset link has been sent.";

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower() && u.IsActive);
                if (user == null)
                {
                    // Don't leak that email doesn't exist
                    _logger.LogInformation("Forgot password requested for non-existent email: {Email}", email);
                    return (true, genericMessage);
                }

                // Check rate limit: count reset tokens created in last hour for this user
                var requestLimitPerHour = await _appConfig.GetIntAsync(AppConfigKeys.PasswordResetRequestLimitPerHour, 3);
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);
                var recentRequests = await _context.PasswordResetTokens
                    .Where(p => p.UserId == user.Id && p.CreatedAt > oneHourAgo && !p.IsUsed)
                    .CountAsync();

                if (recentRequests >= requestLimitPerHour)
                {
                    _logger.LogWarning("Rate limit exceeded for forgot password: {Email}, requests: {Count}", email, recentRequests);
                    return (true, genericMessage); // Still return success to client
                }

                // Generate secure token
                var tokenBytes = new byte[32];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                var plainToken = Convert.ToBase64String(tokenBytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
                var hashedToken = HashResetToken(plainToken);

                // Get token validity duration
                var validityMinutes = await _appConfig.GetIntAsync(AppConfigKeys.PasswordResetTokenValidityMinutes, 15);
                var expiresAt = DateTime.UtcNow.AddMinutes(validityMinutes);

                // Store token in database
                var resetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = hashedToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IsUsed = false
                };

                _context.PasswordResetTokens.Add(resetToken);
                await _context.SaveChangesAsync();

                // Build reset URL from domain and path
                var domain = await _appConfig.GetStringAsync(AppConfigKeys.AppDomain, "");
                var path = await _appConfig.GetStringAsync(AppConfigKeys.ForgotPasswordResetUrlPath, "");
                
                if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(path))
                {
                    _logger.LogWarning("AppDomain and/or ForgotPasswordResetUrlPath not configured in AppConfig");
                    return (true, genericMessage); // Don't send email if URL not configured
                }

                var resetUrl = $"{domain}{path}".Replace("{token}", plainToken);

                // Send email
                var (html, plain) = await _emailTemplates.BuildPasswordResetRequestEmailAsync(user.FirstName, resetUrl, validityMinutes);
                
                var emailSent = await _emailService.SendEmailAsync(
                    user.Email,
                    $"{await _appConfig.GetStringAsync(AppConfigKeys.AppName, "App")}: Password Reset Request",
                    html,
                    plain);

                if (!emailSent)
                {
                    _logger.LogWarning("Failed to send password reset email to {Email}", user.Email);
                    // Still return success to client - caller won't know email failed
                }

                _logger.LogInformation("Forgot password reset token created for user: {UserId}", user.Id);
                return (true, genericMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ForgotPasswordAsync for email: {Email}", email);
                return (true, genericMessage); // Don't leak exception details to client
            }
        }

        /// <summary>
        /// Verifies if a password reset token is valid and not expired.
        /// </summary>
        public async Task<(bool isValid, int? userId, string? email)> VerifyResetTokenAsync(string plainToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(plainToken))
                {
                    return (false, null, null);
                }

                // Hash the provided token to compare with DB
                var hashedToken = HashResetToken(plainToken);

                // Find the token in database
                var token = await _context.PasswordResetTokens
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Token == hashedToken);

                if (token == null)
                {
                    _logger.LogWarning("Password reset token not found in database");
                    return (false, null, null);
                }

                // Check if expired
                if (token.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Password reset token has expired. TokenId: {TokenId}", token.Id);
                    return (false, null, null);
                }

                // Check if already used
                if (token.IsUsed)
                {
                    _logger.LogWarning("Password reset token already used. TokenId: {TokenId}", token.Id);
                    return (false, null, null);
                }

                return (true, token.UserId, token.User?.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying password reset token");
                return (false, null, null);
            }
        }

        /// <summary>
        /// Resets a user's password using a valid reset token.
        /// </summary>
        public async Task<(bool success, string? error, string? jwtToken)> ResetPasswordAsync(string plainToken, string newPassword)
        {
            try
            {
                // Verify token validity
                var (isValid, userId, email) = await VerifyResetTokenAsync(plainToken);
                if (!isValid || !userId.HasValue)
                {
                    return (false, "Invalid or expired password reset token", null);
                }

                // Validate new password
                var passwordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, passwordRegex))
                {
                    return (false, "Password must be at least 8 characters long and contain uppercase, lowercase, and numeric characters", null);
                }

                // Get the user and token
                var user = await _context.Users.FindAsync(userId.Value);
                if (user == null)
                {
                    return (false, "User not found", null);
                }

                var hashedToken = HashResetToken(plainToken);
                var token = await _context.PasswordResetTokens
                    .FirstOrDefaultAsync(p => p.Token == hashedToken && !p.IsUsed);

                if (token == null)
                {
                    return (false, "Reset token not found", null);
                }

                // Update user password
                user.PasswordHash = _passwordHashingService.HashPassword(newPassword);
                user.PasswordLastChanged = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;

                // Mark token as used
                token.IsUsed = true;
                token.UsedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send confirmation email
                var (confirmationHtml, confirmationPlain) = await _emailTemplates.BuildPasswordResetSuccessEmailAsync(user.FirstName);
                await _emailService.SendEmailAsync(
                    user.Email,
                    $"{await _appConfig.GetStringAsync(AppConfigKeys.AppName, "App")}: Password Reset Successful",
                    confirmationHtml,
                    confirmationPlain);

                // Generate JWT token for auto-login
                var (jwtToken, _) = GenerateJwtToken(user);

                _logger.LogInformation("Password reset successful for user: {UserId}", user.Id);
                return (true, null, jwtToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPasswordAsync");
                return (false, "An error occurred while resetting password", null);
            }
        }
    }
}

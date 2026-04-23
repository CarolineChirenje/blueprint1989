using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Blueprint1989.Api.Data;
using Blueprint1989.Api.DTOs.Auth;
using Blueprint1989.Api.DTOs.User;
using Blueprint1989.Api.Models;
using Blueprint1989.Api.Services;

namespace Blueprint1989.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly MfaService _mfaService;
        private readonly IConfiguration _configuration;
        private readonly TimeZoneService _timeZoneService;
        private readonly ApplicationDbContext _context;

        public AuthController(AuthService authService, MfaService mfaService, IConfiguration configuration, TimeZoneService timeZoneService, ApplicationDbContext context)
        {
            _authService = authService;
            _mfaService = mfaService;
            _configuration = configuration;
            _timeZoneService = timeZoneService;
            _context = context;
        }

        [HttpPost("signup")]
        [AllowAnonymous]
        public async Task<IActionResult> Signup([FromBody] SignupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "All fields are required" });
            }

            // Only allow self-registration for these roles
            var allowedRoles = new[] { Models.Role.Admin, Models.Role.User };
            if (!allowedRoles.Contains(request.Role))
            {
                return BadRequest(new { message = "Invalid role selection" });
            }

            // Email format validation
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return BadRequest(new { message = "Invalid email format" });
            }

            // Password strength validation: min 8 chars, upper, lower, digit
            if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$"))
            {
                return BadRequest(new { message = "Password must be at least 8 characters and include uppercase, lowercase, and a number" });
            }

            // Check for duplicate email
            var emailExists = await _authService.EmailExistsAsync(request.Email);
            if (emailExists)
            {
                return BadRequest(new { message = "An account with this email already exists" });
            }

            // Check for duplicate first+last name
            var nameExists = await _authService.NameExistsAsync(request.FirstName, request.LastName);
            if (nameExists)
            {
                return BadRequest(new { message = "An account with this first and last name already exists" });
            }

            var (user, error) = await _authService.SignupAsync(request);
            if (user == null)
            {
                return BadRequest(new { message = error ?? "Signup failed" });
            }

            return Ok(new { message = "Signup successful", userId = user.Id, skipMfaToken = GenerateTempToken(user.Id) });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginByEmail([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email and password are required" });
            }

            var (user, token, tokenExpiresAt, requiresMfa, passwordExpired, emailUnverified) = await _authService.LoginByEmailAsync(request.Email, request.Password);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            if (emailUnverified)
            {
                return Ok(new { emailUnverified = true, message = "Please verify your email address before logging in." });
            }

            var response = new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Token = token ?? string.Empty,
                TokenExpiresAt = _timeZoneService.ConvertFromUtc(tokenExpiresAt),
                IsMfaRequired = requiresMfa,
                IsMfaEnabled = user.IsMfaEnabled,
                MfaEnabledAt = _timeZoneService.ConvertFromUtc(user.MfaEnabledAt),
                MfaTempToken = requiresMfa ? GenerateTempToken(user.Id) : null,
                PasswordExpired = passwordExpired,
                TourCompleted = user.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpPost("verify-mfa")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
        {
            if (string.IsNullOrEmpty(request.Code))
            {
                return BadRequest(new { message = "MFA code is required" });
            }

            var (success, token, tokenExpiresAt, error) = await _authService.VerifyMfaAsync(request.UserId, request.Code);
            if (!success)
            {
                if (error == "email_unverified")
                    return StatusCode(403, new { message = "Please verify your email address before completing sign in.", emailUnverified = true });
                return Unauthorized(new { message = error ?? "Unauthorised" });
            }

            var authenticatedUser = await _authService.GetUserByIdAsync(request.UserId);

            var response = new AuthResponse
            {
                Id = authenticatedUser?.Id ?? 0,
                Email = authenticatedUser?.Email ?? "",
                FirstName = authenticatedUser?.FirstName ?? "",
                LastName = authenticatedUser?.LastName ?? "",
                PhoneNumber = authenticatedUser?.PhoneNumber,
                Role = authenticatedUser?.Role ?? Models.Role.User,
                IsMfaEnabled = authenticatedUser?.IsMfaEnabled ?? false,
                MfaEnabledAt = _timeZoneService.ConvertFromUtc(authenticatedUser?.MfaEnabledAt),
                Token = token ?? string.Empty,
                TokenExpiresAt = _timeZoneService.ConvertFromUtc(tokenExpiresAt),
                TourCompleted = authenticatedUser?.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpPost("change-expired-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ChangeExpiredPassword([FromBody] ChangeExpiredPasswordRequest request)
        {
            var (success, error, user, token, tokenExpiresAt) = await _authService.ChangeExpiredPasswordAsync(
                request.UserId, 
                request.CurrentPassword, 
                request.NewPassword
            );

            if (!success || user == null)
            {
                return BadRequest(new { message = error ?? "Request failed" });
            }

            var response = new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Token = token ?? string.Empty,
                TokenExpiresAt = _timeZoneService.ConvertFromUtc(tokenExpiresAt),
                IsMfaEnabled = user.IsMfaEnabled,
                MfaEnabledAt = _timeZoneService.ConvertFromUtc(user.MfaEnabledAt),
                PasswordExpired = false,
                TourCompleted = user.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpPost("skip-mfa-setup")]
        [AllowAnonymous]
        public async Task<IActionResult> SkipMfaSetup([FromBody] SkipMfaSetupRequest request)
        {
            // Validate the short-lived temp token returned at signup time
            var mfaKey = Environment.GetEnvironmentVariable("MFA_TEMP_KEY") ??
                         _configuration["Jwt:MfaTempKey"] ??
                         "MfaTempTokenKey2026SecureForTwoFactorAuthMinimum32CharsLongRequired7X";
            try
            {
                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var key = System.Text.Encoding.UTF8.GetBytes(mfaKey);
                tokenHandler.ValidateToken(request.SkipToken, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out var validatedToken);

                var jwtToken = (System.IdentityModel.Tokens.Jwt.JwtSecurityToken)validatedToken;
                var tokenUserId = int.Parse(jwtToken.Claims.First(c => c.Type == "id").Value);
                if (tokenUserId != request.UserId)
                    return Unauthorized(new { message = "Token does not match the requested user" });
            }
            catch
            {
                return Unauthorized(new { message = "Invalid or expired skip token" });
            }

            await _authService.SkipMfaSetupAsync(request.UserId);
            return Ok(new { message = "MFA setup skipped" });
        }

        [HttpPost("mfa/setup")]
        [Authorize]
        public async Task<IActionResult> SetupMfa()
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid user" });
            }

            var (secret, qrCode, backupCodes) = await _authService.EnableMfaAsync(userId);
            if (secret == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new MfaSetupResponse
            {
                Secret = secret,
                QrCode = qrCode ?? "",
                BackupCodes = backupCodes
            });
        }

        [HttpPost("mfa/confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmMfa([FromBody] ConfirmMfaRequest request)
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid user" });
            }

            var (success, error) = await _authService.ConfirmMfaAsync(userId, request.Secret, request.VerificationCode, request.BackupCodes);
            if (!success)
            {
                return BadRequest(new { message = error ?? "Request failed" });
            }

            // Retrieve updated user info to return to frontend
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new { message = "User not found after enabling MFA" });
            }

            var response = new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Token = string.Empty, // Not needed for this response
                TokenExpiresAt = null,
                IsMfaRequired = false,
                IsMfaEnabled = user.IsMfaEnabled,
                MfaEnabledAt = _timeZoneService.ConvertFromUtc(user.MfaEnabledAt),
                MfaTempToken = null,
                PasswordExpired = false,
                TourCompleted = user.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpPost("mfa/disable")]
        [Authorize]
        public async Task<IActionResult> DisableMfa()
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid user" });
            }

            var (success, error) = await _authService.DisableMfaAsync(userId);
            if (!success)
            {
                return BadRequest(new { message = error ?? "Request failed" });
            }

            // Retrieve updated user info to return to frontend
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new { message = "User not found after disabling MFA" });
            }

            var response = new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Token = string.Empty, // Not needed for this response
                TokenExpiresAt = null,
                IsMfaRequired = false,
                IsMfaEnabled = user.IsMfaEnabled,
                MfaEnabledAt = _timeZoneService.ConvertFromUtc(user.MfaEnabledAt),
                MfaTempToken = null,
                PasswordExpired = false,
                TourCompleted = user.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpGet("users")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _authService.GetAllUsersAsync();
            var userDtos = users.Select(u => new UserManagementDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                FullName = GetFullName(u.FirstName, u.LastName),
                Role = u.Role,
                RoleName = GetRoleName(u.Role),
                IsActive = u.IsActive,
                ActiveStatus = u.IsActive ? "Yes" : "No",
                IsMfaEnabled = u.IsMfaEnabled,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = _timeZoneService.ConvertFromUtc(u.CreatedAt),
            }).ToList();

            return Ok(userDtos);
        }



        private string GetRoleName(Role role) => role switch
        {
            Role.SuperAdmin => "Super Admin",
            Role.Admin      => "Admin",
            Role.User       => "User",
            _ => "Unknown"
        };

        private string GetFullName(string? firstName, string? lastName)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(firstName)) parts.Add(firstName);
            if (!string.IsNullOrWhiteSpace(lastName)) parts.Add(lastName);
            return parts.Count > 0 ? string.Join(" ", parts) : "-";
        }

        [HttpPut("users/{userId}/status")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> UpdateUserStatus(int userId, [FromBody] UpdateUserStatusRequest request)
        {
            var (success, error) = await _authService.UpdateUserStatusAsync(userId, request.IsActive);
            if (!success)
            {
                return NotFound(new { message = error ?? "Request failed" });
            }

            return Ok(new { message = "User status updated successfully" });
        }

        [HttpPut("users/{userId}")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
        {
            if (userId != request.UserId)
            {
                return BadRequest(new { message = "User ID in URL does not match request body" });
            }

            var (success, error) = await _authService.UpdateUserAsync(
                userId,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role
            );

            if (!success)
            {
                return BadRequest(new { message = error });
            }

            return Ok(new { message = "User updated successfully" });
        }

        [HttpDelete("users/{userId}")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var (success, error) = await _authService.DeleteUserAsync(userId);
            if (!success)
            {
                return NotFound(new { message = error ?? "Request failed" });
            }

            return Ok(new { message = "User deleted successfully" });
        }

        [HttpPut("users/{userId}/verify-email")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> AdminVerifyEmail(int userId)
        {
            var adminId = int.Parse(User.FindFirst("id")!.Value);
            var (success, error) = await _authService.AdminVerifyEmailAsync(adminId, userId);
            if (!success)
                return BadRequest(new { message = error });

            return Ok(new { message = "User email verified successfully" });
        }

        [HttpPut("users/{userId}/reset-password")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> AdminResetPassword(int userId, [FromBody] AdminResetPasswordRequest request)
        {
            if (userId != request.UserId)
                return BadRequest(new { message = "User ID in URL does not match request body" });

            var adminId = int.Parse(User.FindFirst("id")!.Value);
            var (success, error) = await _authService.AdminResetPasswordAsync(adminId, userId, request.NewPassword);
            if (!success)
                return BadRequest(new { message = error });

            return Ok(new { message = "User password reset successfully" });
        }

        [HttpPost("register")]
        [Authorize(Policy = "AdminOrAbove")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email and password are required" });
            }

            var (user, error) = await _authService.RegisterAsync(request.Email, request.Password, request.Role);
            if (user == null)
            {
                return BadRequest(new { message = error ?? "Request failed" });
            }

            var token = GenerateTempToken(user.Id);
            var response = new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role,
                Token = token,
                TourCompleted = user.TourCompletedAt != null
            };

            return Ok(response);
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "Invalid user" });
            }

            var (success, error) = await _authService.UpdateProfileAsync(
                userId, 
                request.FirstName, 
                request.LastName,
                request.PhoneNumber,
                request.CurrentPassword, 
                request.NewPassword
            );

            if (!success)
            {
                return BadRequest(new { message = error ?? "Request failed" });
            }

            return Ok(new { message = "Profile updated successfully" });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            var (success, message) = await _authService.ForgotPasswordAsync(request.Email);
            return Ok(new { success, message });
        }

        [HttpGet("reset-password/validate-token")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { message = "Token is required" });
            }

            var (isValid, userId, email) = await _authService.VerifyResetTokenAsync(token);
            return Ok(new ValidateResetTokenResponse { Valid = isValid, Email = email });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Token and new password are required" });
            }

            // Resolve user before the token is consumed; after reset it is marked IsUsed
            var (preValid, preUserId, _) = await _authService.VerifyResetTokenAsync(request.Token);
            if (!preValid || !preUserId.HasValue)
            {
                return BadRequest(new { success = false, error = "Invalid or expired password reset token" });
            }

            var user = await _context.Users.FindAsync(preUserId.Value);
            var (success, error, jwtToken) = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            if (!success)
            {
                return BadRequest(new { success, error });
            }

            var response = new ResetPasswordResponse
            {
                Success = true,
                Token = jwtToken,
                User = user != null ? new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role.ToString()
                } : null
            };

            return Ok(response);
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { success = false, error = "Token is required" });

            var (success, error) = await _authService.VerifyEmailAsync(token);
            if (!success)
                return BadRequest(new { success = false, error });

            return Ok(new { success = true, message = "Your email has been verified. You can now log in." });
        }

        [HttpPost("resend-verification-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerificationEmail([FromBody] ForgotPasswordRequest request)
        {
            const string genericMessage = "If your account exists and is unverified, a new verification link has been sent.";

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var user = await _context.Users.FirstOrDefaultAsync(
                    u => u.Email.ToLower() == request.Email.Trim().ToLower() && u.IsActive && !u.IsEmailVerified);

                if (user != null)
                {
                    try { await _authService.SendVerificationEmailAsync(user); }
                    catch { /* logged inside service */ }
                }
            }

            return Ok(new { message = genericMessage });
        }

        private string GenerateTempToken(int userId)
        {
            // Temporary token used just for MFA verification
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var mfaKey = Environment.GetEnvironmentVariable("MFA_TEMP_KEY") ?? 
                        _configuration["Jwt:MfaTempKey"] ??
                        "MfaTempTokenKey2026SecureForTwoFactorAuthMinimum32CharsLongRequired7X";
            var key = System.Text.Encoding.UTF8.GetBytes(mfaKey);
            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("id", userId.ToString()) }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}

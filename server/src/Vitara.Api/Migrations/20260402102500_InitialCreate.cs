using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Divvy.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfigEntries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    is_read_only = table.Column<bool>(type: "boolean", nullable: false),
                    requires_restart = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_config_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTypes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_admin_controlled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_last_changed = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_mfa_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    mfa_secret = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    mfa_enabled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    backup_codes_json = table.Column<string>(type: "text", nullable: false),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationTokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_id1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_verification_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_email_verification_tokens_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCycles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_cycles", x => x.id);
                    table.ForeignKey(
                        name: "fk_expense_cycles_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    user_id1 = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    deep_link_url = table.Column<string>(type: "text", nullable: true),
                    related_entity_id = table.Column<int>(type: "integer", nullable: true),
                    sent_via_push = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_id1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_password_reset_tokens_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    user_id1 = table.Column<int>(type: "integer", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    p256dh = table.Column<string>(type: "text", nullable: false),
                    auth = table.Column<string>(type: "text", nullable: false),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_push_subscriptions_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    user_id1 = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    device_type = table.Column<int>(type: "integer", nullable: false),
                    device_model = table.Column<string>(type: "text", nullable: true),
                    device_manufacturer = table.Column<string>(type: "text", nullable: true),
                    os_version = table.Column<string>(type: "text", nullable: true),
                    app_version = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    install_status = table.Column<int>(type: "integer", nullable: false),
                    next_prompt_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_devices_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    notification_type_id = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    user_id1 = table.Column<int>(type: "integer", nullable: false),
                    notification_type_id1 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_preferences", x => new { x.user_id, x.notification_type_id });
                    table.ForeignKey(
                        name: "fk_user_notification_preferences_notification_types_notification_",
                        column: x => x.notification_type_id,
                        principalTable: "NotificationTypes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_notification_preferences_notification_types_notificati~",
                        column: x => x.notification_type_id1,
                        principalTable: "NotificationTypes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_notification_preferences_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CycleMembers",
                columns: table => new
                {
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cycle_members", x => new { x.expense_cycle_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_cycle_members_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cycle_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    paid_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.ForeignKey(
                        name: "fk_expenses_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_expenses_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    payer_id = table.Column<int>(type: "integer", nullable: false),
                    payee_id = table.Column<int>(type: "integer", nullable: false),
                    expense_cycle_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_payments_expense_cycles_expense_cycle_id",
                        column: x => x.expense_cycle_id,
                        principalTable: "ExpenseCycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_payments_users_payee_id",
                        column: x => x.payee_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_users_payer_id",
                        column: x => x.payer_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebAuthnCredentials",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    credential_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    aaguid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    device_friendly_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_device_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    user_id1 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_web_authn_credentials", x => x.id);
                    table.ForeignKey(
                        name: "fk_web_authn_credentials_user_devices_user_device_id",
                        column: x => x.user_device_id,
                        principalTable: "UserDevices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_web_authn_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_web_authn_credentials_users_user_id1",
                        column: x => x.user_id1,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberObligations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    expense_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    amount_owed = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_settled = table.Column<bool>(type: "boolean", nullable: false),
                    settled_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_obligations", x => x.id);
                    table.ForeignKey(
                        name: "fk_member_obligations_expenses_expense_id",
                        column: x => x.expense_id,
                        principalTable: "Expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_member_obligations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_config_entries_key",
                table: "AppConfigEntries",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cycle_members_user_id",
                table: "CycleMembers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_token",
                table: "EmailVerificationTokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user_id",
                table: "EmailVerificationTokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user_id1",
                table: "EmailVerificationTokens",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_expense_cycles_created_by_user_id",
                table: "ExpenseCycles",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_expense_cycle_id",
                table: "Expenses",
                column: "expense_cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_paid_by_user_id",
                table: "Expenses",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_obligations_expense_id",
                table: "MemberObligations",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_member_obligations_user_id",
                table: "MemberObligations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "Notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id1",
                table: "Notifications",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_token",
                table: "PasswordResetTokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id",
                table: "PasswordResetTokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_password_reset_tokens_user_id1",
                table: "PasswordResetTokens",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_payments_expense_cycle_id",
                table: "Payments",
                column: "expense_cycle_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payee_id",
                table: "Payments",
                column: "payee_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_payer_id",
                table: "Payments",
                column: "payer_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id",
                table: "PushSubscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id1",
                table: "PushSubscriptions",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "Roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_user_id",
                table: "UserDevices",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_user_id1",
                table: "UserDevices",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_notification_type_id",
                table: "UserNotificationPreferences",
                column: "notification_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_notification_type_id1",
                table: "UserNotificationPreferences",
                column: "notification_type_id1");

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_preferences_user_id1",
                table: "UserNotificationPreferences",
                column: "user_id1");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "Users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_web_authn_credentials_user_device_id",
                table: "WebAuthnCredentials",
                column: "user_device_id");

            migrationBuilder.CreateIndex(
                name: "ix_web_authn_credentials_user_id",
                table: "WebAuthnCredentials",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_web_authn_credentials_user_id1",
                table: "WebAuthnCredentials",
                column: "user_id1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConfigEntries");

            migrationBuilder.DropTable(
                name: "CycleMembers");

            migrationBuilder.DropTable(
                name: "EmailVerificationTokens");

            migrationBuilder.DropTable(
                name: "MemberObligations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");

            migrationBuilder.DropTable(
                name: "WebAuthnCredentials");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "NotificationTypes");

            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.DropTable(
                name: "ExpenseCycles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

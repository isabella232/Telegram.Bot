// ReSharper disable PossibleNullReferenceException
// ReSharper disable CheckNamespace

using System.Security.Cryptography;
using System.Threading.Tasks;
using IntegrationTests.Framework;
using Telegram.Bot;
using Telegram.Bot.Passport;
using Telegram.Bot.Passport.Request;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Passport;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace IntegrationTests
{
    [Collection(Constants.TestCollections.DriverLicenseErrors)]
    [TestCaseOrderer(Constants.TestCaseOrderer, Constants.AssemblyName)]
    public class DriverLicenseErrorTests : IClassFixture<DriverLicenseErrorTests.Fixture>
    {
        private ITelegramBotClient BotClient => _fixture.BotClient;

        private readonly TestsFixture _fixture;

        private readonly Fixture _classFixture;

        public DriverLicenseErrorTests(TestsFixture fixture, Fixture classFixture)
        {
            _fixture = fixture;
            _classFixture = classFixture;
        }

        [OrderedFact("Should generate passport authorization request link")]
        public async Task Should_generate_auth_link()
        {
            const string publicKey = "-----BEGIN PUBLIC KEY-----\n" +
                                     "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0VElWoQA2SK1csG2/sY/\n" +
                                     "wlssO1bjXRx+t+JlIgS6jLPCefyCAcZBv7ElcSPJQIPEXNwN2XdnTc2wEIjZ8bTg\n" +
                                     "BlBqXppj471bJeX8Mi2uAxAqOUDuvGuqth+mq7DMqol3MNH5P9FO6li7nZxI1FX3\n" +
                                     "9u2r/4H4PXRiWx13gsVQRL6Clq2jcXFHc9CvNaCQEJX95jgQFAybal216EwlnnVV\n" +
                                     "giT/TNsfFjW41XJZsHUny9k+dAfyPzqAk54cgrvjgAHJayDWjapq90Fm/+e/DVQ6\n" +
                                     "BHGkV0POQMkkBrvvhAIQu222j+03frm9b2yZrhX/qS01lyjW4VaQytGV0wlewV6B\n" +
                                     "FwIDAQAB\n" +
                                     "-----END PUBLIC KEY-----";

            PassportScope scope = new PassportScope
            {
                Data = new[]
                {
                    new PassportScopeElementOne(PassportEnums.Scope.DriverLicense)
                    {
                        Selfie = true,
                        Translation = true,
                    },
                }
            };
            AuthorizationRequest authReq = new AuthorizationRequest(
                botId: _fixture.BotUser.Id,
                publicKey: publicKey,
                nonce: "TEST",
                scope: scope
            );

            await BotClient.SendTextMessageAsync(
                _fixture.SupergroupChat,
                "Share your *driver license with its translation and a selfie* using Passport.\n\n" +
                "1. Click inline button\n" +
                "2. Open link in browser to redirect you back to Telegram passport\n" +
                "3. Authorize bot to access the info",
                ParseMode.Markdown,
                replyMarkup: (InlineKeyboardMarkup) InlineKeyboardButton.WithUrl(
                    "Share via Passport",
                    $"https://telegrambots.github.io/Telegram.Bot.Extensions.Passport/redirect.html?{authReq.Query}"
                )
            );

            Update[] updates = await _fixture.UpdateReceiver.GetUpdatesAsync(
                u => u.Message?.PassportData != null,
                updateTypes: UpdateType.Message
            );

            Update passportUpdate = Assert.Single(updates);

            RSA key = EncryptionKey.ReadAsRsa();
            IDecrypter decrypter = new Decrypter(key);
            Credentials credentials = decrypter.DecryptCredentials(passportUpdate.Message.PassportData.Credentials);

            Assert.Equal("TEST", credentials.Nonce);

            _classFixture.AuthorizationRequest = authReq;
            _classFixture.Credentials = credentials;
            _classFixture.Message = passportUpdate.Message;
        }

        [OrderedFact("Should set error for selfie of deriver license")]
        public async Task Should_set_error_selfie()
        {
            AuthorizationRequest authReq = _classFixture.AuthorizationRequest;
            Credentials credentials = _classFixture.Credentials;
            Message passportMessage = _classFixture.Message;

            PassportElementError[] errors =
            {
                new PassportElementErrorDataField(
                    PassportEnums.Scope.DriverLicense,
                    "document_no",
                    credentials.SecureData.DriverLicense.Data.DataHash,
                    "Invalid license number."
                ),
                new PassportElementErrorFrontSide(
                    PassportEnums.Scope.DriverLicense,
                    credentials.SecureData.DriverLicense.FrontSide.FileHash,
                    "Document scan is redacted."
                ),
                new PassportElementErrorReverseSide(
                    PassportEnums.Scope.DriverLicense,
                    credentials.SecureData.DriverLicense.ReverseSide.FileHash,
                    "Bar code is not readable."
                ),
                new PassportElementErrorSelfie(
                    PassportEnums.Scope.DriverLicense,
                    credentials.SecureData.DriverLicense.Selfie.FileHash,
                    "This is Mr. Bean. Not you!"
                ),
                new PassportElementErrorTranslationFile(
                    PassportEnums.Scope.DriverLicense,
                    credentials.SecureData.DriverLicense.Translation[0].FileHash,
                    "Document photo is blury."
                ),
            };

            await BotClient.SetPassportDataErrorsAsync(
                passportMessage.From.Id,
                errors
            );

            await BotClient.SendTextMessageAsync(
                _fixture.SupergroupChat,
                "Errors are set on all driver license data.\n" +
                "You can see error message with opening the request link again.",
                replyMarkup: (InlineKeyboardMarkup) InlineKeyboardButton.WithUrl(
                    "Passport Authorization Request",
                    $"https://telegrambots.github.io/Telegram.Bot.Extensions.Passport/redirect.html?{authReq.Query}"
                )
            );
        }

        public class Fixture
        {
            public Message Message;

            public Credentials Credentials;

            public AuthorizationRequest AuthorizationRequest;
        }
    }
}

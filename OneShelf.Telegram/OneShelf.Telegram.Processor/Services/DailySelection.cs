using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneShelf.Common.Database.Songs;
using OneShelf.Common.Database.Songs.Model;
using OneShelf.Common.Database.Songs.Model.Enums;
using OneShelf.Telegram.Helpers;
using OneShelf.Telegram.Processor.Helpers;
using OneShelf.Telegram.Processor.Model;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableMethods;

namespace OneShelf.Telegram.Processor.Services
{
    public class DailySelection
    {
        private readonly ILogger<DailySelection> _logger;
        private readonly SongsDatabase _songsDatabase;
        private readonly TelegramOptions _options;

        public DailySelection(ILogger<DailySelection> logger, SongsDatabase songsDatabase, IOptions<TelegramOptions> options)
        {
            _logger = logger;
            _songsDatabase = songsDatabase;
            _options = options.Value;
        }

        public async Task DailySelectionGo()
        {
            var users = await GetUsers();

            var (available, askedNever) = await GetAvailableUsers();

            var two = available.OrderBy(_ => Random.Shared.NextDouble()).Take(2).ToList();

            foreach (var user in users)
            {
                user.Value.AuthorizedToUseIllustrationAlterationsTemporarilySince = two.Contains(user.Key) ? DateTime.Now : null;
            }

            await _songsDatabase.SaveChangesAsyncX();
            
            await SendInvitation(two[0], available.Count, two[1], users[two[1]].Title);
            await SendInvitation(two[1], available.Count, two[0], users[two[0]].Title);
        }

        public async Task<string> DailySelectionTest()
        {
            var users = await GetUsers();

            var (available, askedNever) = await GetAvailableUsers();

            var random = available.MinBy(_ => Random.Shared.NextDouble());
            await SendInvitation(_options.AdminId, available.Count, random, users[random].Title);

            return $"available: {available.Count} people, asked never: {askedNever.Count} people: {string.Join(", ", askedNever.Select(x => users[x].Title))}";
        }

        private async Task<Dictionary<long, User>> GetUsers()
        {
            return await _songsDatabase.Users
                .Where(x => x.TenantId == _options.TenantId)
                .ToDictionaryAsync(x => x.Id);
        }

        private async Task<(List<long> available, List<long> askedNever)> GetAvailableUsers()
        {
            var interactions = await _songsDatabase.Interactions
                .Where(x => x.User.TenantId == _options.TenantId)
                .Where(x => x.InteractionType == InteractionType.SongImages ||
                            x.InteractionType == InteractionType.NeverPromoteTopics)
                .GroupBy(x => new { x.InteractionType, x.UserId, })
                .Select(x => x.Key)
                .ToListAsync();

            var askedNever = interactions.Where(x => x.InteractionType == InteractionType.NeverPromoteTopics).Select(x => x.UserId).ToList();
            var takenPart = interactions.Where(x => x.InteractionType == InteractionType.SongImages).Select(x => x.UserId).ToList();

            return (takenPart.Except(askedNever).ToList(), askedNever);
        }

        private async Task SendInvitation(long userId, int total, long anotherId, string anotherTitle)
        {
            try
            {
                var botClient = new TelegramBotClient(_options.Token);

                var message = $@"

Гав! 🐾

Каждый день, в 12:34, я выбираю двух случайных людей из всех, которые когда-нибудь генерировали картинки к песням ({total} человек на данный момент).
Я никому не говорю, кого выбрала, только им лично.

Сегодня 🩵🩵🩵 — я выбрала вас и
".Trim().ToMarkdown();
                message.Append(" ".ToMarkdown());
                message.Append(anotherId.BuildUrl(anotherTitle));
                message.Append($@"

.

Следующие 24 часа, вы двое можете делать необычные картинки к песням. 😈😭😱
Используйте команду /song_images, выбирайте пятую версию, и там будет список тем.
Пятую версию, запомнили?
Если хотите — то пользуйтесь. В 12:34 завтрашнего дня эта возможность пропадёт, как тыква. :)

Сегодня вероятность, что я выберу вас, составляла {(int)(2f / total * 100)}%.
Если я потревожила вас и вы никогда не хотите, чтобы в будущем я выбирала вас, то используйте команду /never_promote.
Тогда я не буду вам присылать эти сообщения.

Удачного дня! 🐾🎶

".Trim().ToMarkdown());

                await botClient.SendMessageAsync(userId, message.ToString(), parseMode: Constants.MarkdownV2);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error sending the invitation.");
            }
        }
    }
}

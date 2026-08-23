using FrancProject.Data;
using FrancProject.Dto;
using FrancProject.Interfaces;
using FrancProject.Models;
using Microsoft.EntityFrameworkCore;

using FrancProject.Helpers;

namespace FrancProject.Services
{
    public class GameQuizService : IGameQuizService
    {
        private readonly DataContext _context;
        private readonly IActivityEventWriter _activityEvents;

        public GameQuizService(DataContext context, IActivityEventWriter activityEvents)
        {
            _context = context;
            _activityEvents = activityEvents;
        }

        public static GameAbilityKind ParseAbilityKind(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Ability is required.");
            return raw.Trim() switch
            {
                "Skip" => GameAbilityKind.Skip,
                "FiftyFifty" => GameAbilityKind.FiftyFifty,
                "DoubleChance" => GameAbilityKind.DoubleChance,
                "TimeFreeze" => GameAbilityKind.TimeFreeze,
                "Hint" => GameAbilityKind.Hint,
                _ => throw new InvalidOperationException("Unknown ability. Use Skip, FiftyFifty, DoubleChance, TimeFreeze, or Hint.")
            };
        }


        public async Task<UserGameProgressDto> GetProgressAsync(int userId)
        {
            await EnsureProgressRowAsync(userId);
            var p = await _context.UserGameProgresses.AsNoTracking()
                .FirstAsync(x => x.UserId == userId);
            // Required for React "Continue quiz": JSON camelCase activeSessionId
            var activeSessionId = await _context.GameSessions.AsNoTracking()
                .Where(s => s.UserId == userId && s.Status == GameQuizConstants.StatusInProgress)
                .Select(s => (long?)s.Id)
                .FirstOrDefaultAsync();
            var levelScores = await LoadLevelBestScoresForProgressAsync(userId);
            return MapProgress(p, activeSessionId, levelScores);
        }
        private async Task<Dictionary<int, int>> LoadLevelBestScoresForProgressAsync(int userId)
        {
            var rows = await _context.GameSessions.AsNoTracking()
                .Where(s => s.UserId == userId && s.FinishedAt != null)
                .Select(s => new { LevelNumber = s.Level.LevelNumber, s.CorrectAnswers })
                .ToListAsync();

            return rows
                .GroupBy(r => r.LevelNumber)
                .ToDictionary(g => g.Key, g => g.Max(x => x.CorrectAnswers));
        }
        public async Task<GameSessionStateDto> StartSessionAsync(int userId, StartGameSessionRequestDto request)
        {
            var levelNumber = request.LevelNumber;
            if (levelNumber is < 1 or > 5)
                throw new InvalidOperationException("LevelNumber must be between 1 and 5.");

            await EnsureProgressRowAsync(userId);

            var hasInProgress = await _context.GameSessions
                .AnyAsync(s => s.UserId == userId && s.Status == GameQuizConstants.StatusInProgress);
            if (hasInProgress)
                throw new InvalidOperationException("You already have a quiz session in progress. Finish or abandon it first.");

            var progress = await _context.UserGameProgresses.FirstAsync(p => p.UserId == userId);
            if (levelNumber > progress.HighestUnlockedLevel)
                throw new InvalidOperationException("This level is not unlocked yet.");

            var level = await _context.GameLevels
                .FirstOrDefaultAsync(l => l.LevelNumber == levelNumber && l.IsActive);
            if (level == null)
                throw new InvalidOperationException("Level not found or inactive.");

            var pool = await _context.GameQuestions
                .Where(q => q.LevelId == level.Id && q.IsActive)
                .ToListAsync();

            if (pool.Count < GameQuizConstants.QuestionsPerSession)
                throw new InvalidOperationException(
                    $"Not enough active questions for this level (need {GameQuizConstants.QuestionsPerSession}, have {pool.Count}).");

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var picked = pool.OrderBy(_ => Guid.NewGuid()).Take(GameQuizConstants.QuestionsPerSession).ToList();
                    var now = DateTimeOffset.UtcNow;
                    var session = new GameSession
                    {
                        UserId = userId,
                        LevelId = level.Id,
                        StartedAt = now,
                        Status = GameQuizConstants.StatusInProgress,
                        Score = 0,
                        CorrectAnswers = 0,
                        WrongAnswers = 0,
                        Passed = false
                    };
                    _context.GameSessions.Add(session);
                    await _context.SaveChangesAsync();

                    var order = 0;
                    foreach (var q in picked)
                    {
                        _context.GameSessionAnswers.Add(new GameSessionAnswer
                        {
                            SessionId = session.Id,
                            QuestionId = q.Id,
                            QuestionOrder = order++,
                            AnswerAttemptCount = 0
                        });
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                    return await GetSessionAsync(userId, session.Id);
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<GameSessionStateDto> GetSessionAsync(int userId, long sessionId)
        {
            var session = await LoadSessionGraphAsync(sessionId, userId, asNoTracking: true);
            return MapSession(session);
        }

        public async Task<GameSessionHintsDto> GetSessionHintsAsync(int userId, long sessionId)
        {
            var session = await LoadSessionGraphAsync(sessionId, userId, asNoTracking: true);
            var items = session.Answers
                .OrderBy(a => a.QuestionOrder)
                .Select(a => new GameQuestionHintItemDto
                {
                    SessionAnswerId = a.Id,
                    QuestionOrder = a.QuestionOrder,
                    QuestionId = a.QuestionId,
                    Hint = a.Question.Hint
                })
                .ToList();
            return new GameSessionHintsDto { SessionId = session.Id, Hints = items };
        }

        public async Task<GameSessionStateDto> SubmitAnswerAsync(int userId, long sessionId, long sessionAnswerId,
                 SubmitGameAnswerRequestDto request)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var session = await LoadSessionGraphAsync(sessionId, userId, asNoTracking: false);
                    if (session.Status != GameQuizConstants.StatusInProgress)
                        throw new InvalidOperationException("This session is no longer active.");

                    var answer = session.Answers.FirstOrDefault(a => a.Id == sessionAnswerId)
                        ?? throw new InvalidOperationException("Answer slot not found.");

                    if (IsResolved(answer))
                        throw new InvalidOperationException("This question is already resolved.");

                    if (answer.UsedSkip)
                        throw new InvalidOperationException("This question was skipped.");

                    var correct = answer.Question.CorrectOption.Trim().ToUpperInvariant();
                    var now = DateTimeOffset.UtcNow;

                    if (request.TimedOut)
                    {
                        var wrongLetter = new[] { "A", "B", "C", "D" }.First(x => x != correct);

                        if (answer.AnswerAttemptCount == 0)
                        {
                            answer.SelectedOption = wrongLetter;
                            answer.AnswerAttemptCount = 1;
                            if (answer.UsedDoubleChance)
                            {
                                answer.IsCorrect = false;
                            }
                            else
                            {
                                answer.IsCorrect = false;
                                answer.AnsweredAt = now;
                            }
                        }
                        else if (answer.AnswerAttemptCount == 1 && answer.UsedDoubleChance && !answer.AnsweredAt.HasValue)
                        {
                            answer.SelectedOption = wrongLetter;
                            answer.AnswerAttemptCount = 2;
                            answer.IsCorrect = false;
                            answer.AnsweredAt = now;
                        }
                        else
                        {
                            throw new InvalidOperationException("No more attempts are allowed for this question.");
                        }
                    }
                    else
                    {
                        var opt = NormalizeOption(request.SelectedOption);
                        if (!GameQuizConstants.IsValidOptionLetter(opt))
                            throw new InvalidOperationException("SelectedOption must be A, B, C, or D.");

                        if (answer.AnswerAttemptCount == 0)
                        {
                            answer.SelectedOption = opt;
                            answer.AnswerAttemptCount = 1;
                            if (string.Equals(opt, correct, StringComparison.Ordinal))
                            {
                                answer.IsCorrect = true;
                                answer.AnsweredAt = now;
                            }
                            else if (answer.UsedDoubleChance)
                            {
                                answer.IsCorrect = false;
                            }
                            else
                            {
                                answer.IsCorrect = false;
                                answer.AnsweredAt = now;
                            }
                        }
                        else if (answer.AnswerAttemptCount == 1 && answer.UsedDoubleChance && !answer.AnsweredAt.HasValue)
                        {
                            answer.SelectedOption = opt;
                            answer.AnswerAttemptCount = 2;
                            answer.IsCorrect = string.Equals(opt, correct, StringComparison.Ordinal);
                            answer.AnsweredAt = now;
                        }
                        else
                        {
                            throw new InvalidOperationException("No more attempts are allowed for this question.");
                        }
                    }

                    RecountSession(session, session.Answers.ToList());
                    await _context.SaveChangesAsync();

                    if (session.Answers.All(IsResolved))
                        await CompleteSessionCoreAsync(session);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return await GetSessionAsync(userId, sessionId);
        }
        public async Task<GameSessionStateDto> UseAbilityAsync(int userId, long sessionId, long sessionAnswerId,
            UseGameAbilityRequestDto request)
        {
            var kind = ParseAbilityKind(request.Ability);

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var session = await LoadSessionGraphAsync(sessionId, userId, asNoTracking: false);
                    if (session.Status != GameQuizConstants.StatusInProgress)
                        throw new InvalidOperationException("This session is no longer active.");

                    var answer = session.Answers.FirstOrDefault(a => a.Id == sessionAnswerId)
                        ?? throw new InvalidOperationException("Answer slot not found.");

                    if (IsResolved(answer))
                        throw new InvalidOperationException("This question is already resolved.");

                    var answers = session.Answers.ToList();

                    if (kind != GameAbilityKind.Skip && answer.AnswerAttemptCount > 0)
                        throw new InvalidOperationException("This ability can only be used before answering.");

                    switch (kind)
                    {
                        case GameAbilityKind.Skip:
                            if (answer.AnswerAttemptCount > 0)
                                throw new InvalidOperationException("Cannot skip after answering.");
                            if (answers.Any(a => a.UsedSkip))
                                throw new InvalidOperationException("Skip has already been used this session.");
                            answer.UsedSkip = true;
                            answer.IsCorrect = false;
                            answer.AnsweredAt = DateTimeOffset.UtcNow;
                            break;

                        case GameAbilityKind.FiftyFifty:
                            if (answer.UsedFiftyFifty)
                                throw new InvalidOperationException("FiftyFifty was already used on this question.");
                            if (answers.Count(a => a.UsedFiftyFifty) >= GameQuizConstants.MaxFiftyFiftyPerSession)
                                throw new InvalidOperationException("No FiftyFifty uses remaining this session.");
                            answer.UsedFiftyFifty = true;
                            break;

                        case GameAbilityKind.DoubleChance:
                            if (answer.UsedDoubleChance)
                                throw new InvalidOperationException("DoubleChance was already used on this question.");
                            if (answers.Count(a => a.UsedDoubleChance) >= GameQuizConstants.MaxDoubleChancePerSession)
                                throw new InvalidOperationException("No DoubleChance uses remaining this session.");
                            answer.UsedDoubleChance = true;
                            break;

                        case GameAbilityKind.TimeFreeze:
                            if (answer.UsedTimeFreeze)
                                throw new InvalidOperationException("TimeFreeze was already used on this question.");
                            if (answers.Count(a => a.UsedTimeFreeze) >= GameQuizConstants.MaxTimeFreezePerSession)
                                throw new InvalidOperationException("No TimeFreeze uses remaining this session.");
                            answer.UsedTimeFreeze = true;
                            break;

                        case GameAbilityKind.Hint:
                            if (answer.UsedHint)
                                throw new InvalidOperationException("Hint was already used on this question.");
                            if (answers.Count(a => a.UsedHint) >= GameQuizConstants.MaxHintPerSession)
                                throw new InvalidOperationException("No Hint uses remaining this session.");
                            answer.UsedHint = true;
                            break;

                        default:
                            throw new InvalidOperationException("Unknown ability.");
                    }

                    RecountSession(session, answers);
                    await _context.SaveChangesAsync();

                    if (session.Answers.All(IsResolved))
                        await CompleteSessionCoreAsync(session);

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return await GetSessionAsync(userId, sessionId);
        }

        public async Task<GameSessionStateDto> FinishSessionAsync(int userId, long sessionId)
        {
            var session = await LoadSessionGraphAsync(sessionId, userId, asNoTracking: false);
            if (session.Status != GameQuizConstants.StatusInProgress)
                return await GetSessionAsync(userId, sessionId);

            if (!session.Answers.All(IsResolved))
                throw new InvalidOperationException("Not all questions are resolved yet.");

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    await CompleteSessionCoreAsync(session);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return await GetSessionAsync(userId, sessionId);
        }

        private async Task CompleteSessionCoreAsync(GameSession session)
        {
            if (session.Status != GameQuizConstants.StatusInProgress)
                return;

            var answers = session.Answers.ToList();
            if (!answers.All(IsResolved))
                return;

            RecountSession(session, answers);
            var level = session.Level;
            var now = DateTimeOffset.UtcNow;
            session.FinishedAt = now;
            session.Passed = session.CorrectAnswers >= level.PassScore;
            session.Status = session.Passed ? GameQuizConstants.StatusCompleted : GameQuizConstants.StatusFailed;

            if (session.Passed)
                await ApplyProgressOnPassAsync(session.UserId, level.LevelNumber);

            var activityType = session.Passed
                ? ActivityEventTypes.GamificationLevelCompleted
                : ActivityEventTypes.GamificationLevelFailed;

            await _activityEvents.TryLogAsync(
                session.UserId,
                AnalyticsConstants.Gamification,
                activityType,
                session.Status.ToLowerInvariant(),
                session.Id.ToString(),
                now.UtcDateTime,
                session.Passed ? level.BadgeName : $"{session.Score} pts");
        }

        private async Task ApplyProgressOnPassAsync(int userId, int levelNumberPassed)
        {
            var prog = await _context.UserGameProgresses.FirstAsync(p => p.UserId == userId);
            var now = DateTimeOffset.UtcNow;

            switch (levelNumberPassed)
            {
                case 1: prog.BronzeBadgeEarned = true; break;
                case 2: prog.SilverBadgeEarned = true; break;
                case 3: prog.GoldBadgeEarned = true; break;
                case 4: prog.PlatinumBadgeEarned = true; break;
                case 5: prog.DiamondBadgeEarned = true; break;
            }

            prog.HighestUnlockedLevel = Math.Min(5, Math.Max(prog.HighestUnlockedLevel, levelNumberPassed + 1));
            prog.CurrentLevel = Math.Min(5, levelNumberPassed + 1);
            prog.TotalPoints += GameQuizConstants.PointsForLevelPass(levelNumberPassed);
            prog.UpdatedAt = now;
        }

        private async Task EnsureProgressRowAsync(int userId)
        {
            var exists = await _context.UserGameProgresses.AnyAsync(p => p.UserId == userId);
            if (exists)
                return;

            var now = DateTimeOffset.UtcNow;
            _context.UserGameProgresses.Add(new UserGameProgress
            {
                UserId = userId,
                CurrentLevel = 1,
                HighestUnlockedLevel = 1,
                BronzeBadgeEarned = false,
                SilverBadgeEarned = false,
                GoldBadgeEarned = false,
                PlatinumBadgeEarned = false,
                DiamondBadgeEarned = false,
                TotalPoints = 0,
                UpdatedAt = now
            });
            await _context.SaveChangesAsync();
        }

        private async Task<GameSession> LoadSessionGraphAsync(long sessionId, int userId, bool asNoTracking)
        {
            IQueryable<GameSession> q = _context.GameSessions
                .Include(s => s.Level)
                .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
                .Where(s => s.Id == sessionId && s.UserId == userId);

            if (asNoTracking)
                q = q.AsNoTracking();

            return await q.FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Session not found.");
        }

        private static bool IsResolved(GameSessionAnswer a) =>
            a.UsedSkip || a.AnsweredAt.HasValue;

        private static void RecountSession(GameSession session, List<GameSessionAnswer> answers)
        {
            session.CorrectAnswers = answers.Count(a => a.AnsweredAt.HasValue && a.IsCorrect);
            session.WrongAnswers = answers.Count(a => a.AnsweredAt.HasValue && !a.IsCorrect && !a.UsedSkip);
            session.Score = session.CorrectAnswers;
        }

        private static string NormalizeOption(string value) =>
            value.Trim().ToUpperInvariant();

        private GameSessionStateDto MapSession(GameSession session)
        {
            var answers = session.Answers.OrderBy(a => a.QuestionOrder).ToList();
            var abilities = CountAbilitiesRemaining(answers);

            var dto = new GameSessionStateDto
            {
                SessionId = session.Id,
                LevelNumber = session.Level.LevelNumber,
                LevelName = session.Level.Name,
                BadgeName = session.Level.BadgeName,
                PassScore = session.Level.PassScore,
                Status = session.Status,
                Score = session.Score,
                CorrectAnswers = session.CorrectAnswers,
                WrongAnswers = session.WrongAnswers,
                ResolvedCount = answers.Count(IsResolved),
                TotalQuestions = GameQuizConstants.QuestionsPerSession,
                StartedAt = session.StartedAt,
                FinishedAt = session.FinishedAt,
                Passed = session.Passed,
                AbilitiesRemaining = abilities,
                Questions = answers.Select(a => MapQuestionClient(a, session.Id)).ToList()
            };
            return dto;
        }

        private GameQuestionClientDto MapQuestionClient(GameSessionAnswer a, long sessionId)
        {
            var q = a.Question;
            var hidden = a.UsedFiftyFifty
                ? GetFiftyFiftyHidden(sessionId, q.Id, q.CorrectOption)
                : new List<string>();

            return new GameQuestionClientDto
            {
                SessionAnswerId = a.Id,
                QuestionOrder = a.QuestionOrder,
                QuestionText = q.QuestionText,
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                HiddenOptions = hidden,
                Hint = a.UsedHint ? q.Hint : null,
                SelectedOption = a.SelectedOption,
                IsResolved = IsResolved(a),
                IsCorrect = a.IsCorrect,
                UsedSkip = a.UsedSkip,
                UsedFiftyFifty = a.UsedFiftyFifty,
                UsedDoubleChance = a.UsedDoubleChance,
                UsedTimeFreeze = a.UsedTimeFreeze,
                UsedHint = a.UsedHint,
                AwaitingDoubleChanceRetry = a.UsedDoubleChance && !a.AnsweredAt.HasValue && a.AnswerAttemptCount == 1
            };
        }

        private static GameAbilitiesRemainingDto CountAbilitiesRemaining(List<GameSessionAnswer> answers)
        {
            return new GameAbilitiesRemainingDto
            {
                Skip = GameQuizConstants.MaxSkipPerSession - answers.Count(a => a.UsedSkip),
                FiftyFifty = GameQuizConstants.MaxFiftyFiftyPerSession - answers.Count(a => a.UsedFiftyFifty),
                DoubleChance = GameQuizConstants.MaxDoubleChancePerSession - answers.Count(a => a.UsedDoubleChance),
                TimeFreeze = GameQuizConstants.MaxTimeFreezePerSession - answers.Count(a => a.UsedTimeFreeze),
                Hint = GameQuizConstants.MaxHintPerSession - answers.Count(a => a.UsedHint)
            };
        }

        private static List<string> GetFiftyFiftyHidden(long sessionId, long questionId, string correct)
        {
            var c = correct.Trim().ToUpperInvariant();
            var wrong = new[] { "A", "B", "C", "D" }.Where(x => x != c).ToList();
            var seed = unchecked((int)(sessionId ^ (questionId * 397) ^ (c.Length > 0 ? c[0] * 13 : 0)));
            var rnd = new Random(seed);
            return wrong.OrderBy(_ => rnd.Next()).Take(2).ToList();
        }

        private static UserGameProgressDto MapProgress(UserGameProgress p, long? activeSessionId,
           Dictionary<int, int>? levelScores) => new()
           {
            CurrentLevel = p.CurrentLevel,
            HighestUnlockedLevel = p.HighestUnlockedLevel,
            BronzeBadgeEarned = p.BronzeBadgeEarned,
            SilverBadgeEarned = p.SilverBadgeEarned,
            GoldBadgeEarned = p.GoldBadgeEarned,
            PlatinumBadgeEarned = p.PlatinumBadgeEarned,
            DiamondBadgeEarned = p.DiamondBadgeEarned,
            TotalPoints = p.TotalPoints,
            UpdatedAt = p.UpdatedAt,
               ActiveSessionId = activeSessionId,
               LevelScores = levelScores
           };
    }
}

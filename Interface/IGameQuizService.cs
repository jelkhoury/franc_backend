using FrancProject.DTOs;

namespace FrancProject.Interfaces
{
    public interface IGameQuizService
    {
        Task<UserGameProgressDto> GetProgressAsync(int userId);
        Task<GameSessionStateDto> StartSessionAsync(int userId, StartGameSessionRequestDto request);
        Task<GameSessionStateDto> GetSessionAsync(int userId, long sessionId);
        Task<GameSessionHintsDto> GetSessionHintsAsync(int userId, long sessionId);
        Task<GameSessionStateDto> SubmitAnswerAsync(int userId, long sessionId, long sessionAnswerId, SubmitGameAnswerRequestDto request);
        Task<GameSessionStateDto> UseAbilityAsync(int userId, long sessionId, long sessionAnswerId, UseGameAbilityRequestDto request);
        Task<GameSessionStateDto> FinishSessionAsync(int userId, long sessionId);
    }
}

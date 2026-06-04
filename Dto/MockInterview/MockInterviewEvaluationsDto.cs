using System.Collections.Generic;

namespace FrancProject.Dto
{
    public class MockInterviewEvaluationsDto
    {
        public int MockInterviewId { get; set; }
        public string MockInterviewTitle { get; set; }
        public bool IsEvaluated { get; set; }
        public int UserId { get; set; }
        public List<MockInterviewAnswerEvaluationDto> Evaluations { get; set; } = new();
        public List<ReportSkillScoreDto> SkillScores { get; set; } = new();
    }

    public class MockInterviewAnswerEvaluationDto
    {
        public int AnswerId { get; set; }
        public int QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string VideoUrl { get; set; }
        public int? EvaluateQuestionId { get; set; }
        public int? EvaluatorId { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public string? Tips { get; set; }
    }
}

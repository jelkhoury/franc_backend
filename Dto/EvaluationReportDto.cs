using System.Collections.Generic;
using System;

namespace FrancProject.Dto
{
    public class EvaluationReportDto
    {
        public int Id { get; set; }
        public float? OverallRating { get; set; }
        public string? SummaryComment { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<AnswerEvaluationDto> Answers { get; set; }
        public List<ReportSkillScoreDto> SkillScores { get; set; } = new();
    }

}

namespace FrancProject.Dto;

public class EvaluationReportRequest
{
    public int UserId { get; set; }
    public List<int> AnswerIds { get; set; } = new();
    public string? SummaryComment { get; set; }
    public List<ReportSkillScoreInputDto> SkillScores { get; set; } = new();
}

public class EvaluateRequest
{
    public int AnswerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Tips { get; set; }
}

public class EvaluateAnswerDto
{
    public int AnswerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Tips { get; set; }
}

public class EvaluateMultipleRequest
{
    public List<EvaluateAnswerDto> Evaluations { get; set; } = new();
}

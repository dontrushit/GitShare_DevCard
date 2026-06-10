using System.Text.Json.Serialization;
using GitShare.Api.Services.JsonConverters;

namespace GitShare.Api.Models;



public class StructuredAuditResponse

{

    public List<ProjectAuditDetail> Projects { get; set; } = [];

    public string CoreEngineeringFocus { get; set; } = string.Empty;



    /// <summary>

    /// Conventional Commits compliant | Descriptive / Non-standard | Unstructured / Low-density

    /// </summary>

    public string GitFormatStandard { get; set; } = string.Empty;



    public string ExperienceProfile { get; set; } = string.Empty;

    public string OpenSourceImpact { get; set; } = string.Empty;

}



public class ProjectAuditDetail

{

    public string RepoName { get; set; } = string.Empty;

    /// <summary>
    /// Production App | Utility / Automation | QA / Testing | DocOps / Knowledge Base
    /// </summary>
    public string ProjectClass { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string LayoutType { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string> KeyFiles { get; set; } = [];

    public string TechnicalDebt { get; set; } = string.Empty;

    public string DebtSeverity { get; set; } = "Warning";

    public string InterviewTrapQuestion { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string> Pros { get; set; } = [];

    [JsonConverter(typeof(FlexibleStringListJsonConverter))]
    public List<string> Cons { get; set; } = [];
}


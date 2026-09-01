namespace Pulsemap.App.Core.Models;

/// <summary>Drives what the guided measurement walk captures at each point: a new deployment has no live target network to measure signal from, so only ambient noise/interference is captured; an audit captures both.</summary>
public enum SurveyType
{
    NewDeployment,
    ExistingNetworkAudit,
}

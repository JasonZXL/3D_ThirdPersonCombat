using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class FinalBossStageThreeTimelineDirector : MonoBehaviour
{
    private enum StageThreeTimelineState
    {
        Idle = 0,
        PlayingSegment = 1,
        WaitingForQTE = 2,
        Completed = 3,
        Failed = 4
    }

    [Header("Timeline Sequence")]
    [SerializeField] private PlayableDirector playableDirector;
    [SerializeField] private TimelineAsset[] timelineSegments;
    [SerializeField] private bool showDebugLogs = true;

    private FinalBossStageThreeQTEController owner;
    private StageThreeTimelineState currentState = StageThreeTimelineState.Idle;
    private int currentSegmentIndex = -1;

    public int CurrentSegmentIndex => currentSegmentIndex;
    public int SegmentCount => timelineSegments != null ? timelineSegments.Length : 0;

    public void Initialize(FinalBossStageThreeQTEController stageOwner)
    {
        owner = stageOwner;
        playableDirector ??= GetComponent<PlayableDirector>();

        if (playableDirector != null)
        {
            playableDirector.stopped -= HandleDirectorStopped;
            playableDirector.stopped += HandleDirectorStopped;
        }
    }

    public void BeginStage()
    {
        currentState = StageThreeTimelineState.Idle;
        currentSegmentIndex = -1;
        PlayNextSegmentOrComplete();
    }

    public void HandleQTESuccess()
    {
        if (currentState == StageThreeTimelineState.Failed || currentState == StageThreeTimelineState.Completed)
            return;

        PlayNextSegmentOrComplete();
    }

    public void HandleQTEFailure()
    {
        currentState = StageThreeTimelineState.Failed;

        if (playableDirector != null && playableDirector.state == PlayState.Playing)
            playableDirector.Stop();
    }

    public void StopAll()
    {
        currentState = StageThreeTimelineState.Idle;
        currentSegmentIndex = -1;

        if (playableDirector != null)
            playableDirector.Stop();
    }

    private void PlayNextSegmentOrComplete()
    {
        int nextSegmentIndex = currentSegmentIndex + 1;

        if (!TryGetSegment(nextSegmentIndex, out TimelineAsset nextSegment))
        {
            currentState = StageThreeTimelineState.Completed;
            owner?.NotifyStageThreeSequenceFinished();
            Log("PlayNextSegmentOrComplete -> no more segments, complete stage");
            return;
        }

        currentSegmentIndex = nextSegmentIndex;
        PlayTimeline(nextSegment);
    }

    private void PlayTimeline(TimelineAsset timelineAsset)
    {
        if (playableDirector == null || timelineAsset == null)
            return;

        currentState = StageThreeTimelineState.PlayingSegment;
        playableDirector.playableAsset = timelineAsset;
        playableDirector.time = 0d;
        playableDirector.Evaluate();
        playableDirector.Play();
        Log($"PlayTimeline -> segmentIndex={currentSegmentIndex}, asset={timelineAsset.name}");
    }

    private void HandleDirectorStopped(PlayableDirector director)
    {
        if (director == null || director != playableDirector)
            return;

        Log($"HandleDirectorStopped -> state={currentState}, segmentIndex={currentSegmentIndex}");

        if (currentState != StageThreeTimelineState.PlayingSegment)
            return;

        if (owner != null && owner.TryBeginQTEForCurrentSegment(currentSegmentIndex))
        {
            currentState = StageThreeTimelineState.WaitingForQTE;
            Log($"HandleDirectorStopped -> waiting for QTE at segment {currentSegmentIndex}");
            return;
        }

        PlayNextSegmentOrComplete();
    }

    private bool TryGetSegment(int index, out TimelineAsset timelineAsset)
    {
        timelineAsset = null;

        if (timelineSegments == null || index < 0 || index >= timelineSegments.Length)
            return false;

        timelineAsset = timelineSegments[index];
        return timelineAsset != null;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[FinalBossStageThreeTimelineDirector] {message}");
    }
}

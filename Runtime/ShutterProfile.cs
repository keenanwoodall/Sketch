using UnityEngine;

public readonly struct ShutterProfile
{
    public readonly AnimationCurve Profile;
    public readonly float Area;

    public ShutterProfile(AnimationCurve profile)
    {
        Profile = profile;
        Area = 0f;

        var sampleCount = 128;
        var sampleWidth = 1f / sampleCount;
        for (int i = 0; i < sampleCount; i++)
        {
            var progress = i / (sampleCount - 1f);
            var sampleHeight = Profile.Evaluate(progress);
            Area += sampleHeight * sampleWidth;
        }
    }

    public ShutterProfile(float shutterOpen, float shutterClose)
    {
        Profile = new AnimationCurve
        {
            keys = new[]
            {
                new Keyframe(0, 0, 0, 0),
                new Keyframe(shutterOpen, 1, 0, 0),
                new Keyframe(shutterClose, 1, 0, 0),
                new Keyframe(1, 0, 0, 0)
            }
        };

        Area = 0;

        var sampleCount = 128;
        var sampleWidth = 1f / sampleCount;
        for (int i = 0; i < sampleCount; i++)
        {
            var progress = i / (sampleCount - 1f);
            var sampleHeight = Profile.Evaluate(progress);
            Area += sampleHeight * sampleWidth;
        }
    }
}

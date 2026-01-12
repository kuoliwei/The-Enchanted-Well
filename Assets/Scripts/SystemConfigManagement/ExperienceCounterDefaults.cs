using System;

public static class ExperienceCounterDefaults
{
    public static ExperienceCounterData Create()
    {
        return new ExperienceCounterData
        {
            totalCount = 0,
            lastUpdated = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
        };
    }
}

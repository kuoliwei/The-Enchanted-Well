public static class SystemConfigDefaults
{
    public static SystemConfigData Create()
    {
        return new SystemConfigData
        {
            wellCalibration = new WellCalibrationConfig
            {
                rotationOffsetDegrees = 45f,
                positionOffset = new PositionOffset
                {
                    x = 0f,
                    y = 0f
                },
                scale = 1.0f
            },

            webSocketConnection = new WebSocketConnectionConfig
            {
                ip = "127.0.0.1",
                port = 8765
            },

            catAppearance = new CatAppearanceConfig
            {
                secondsToRevealFullBody = 3f,
                SecondsPersonLeavesTemporarily = 1f,
                SecondsPersonLeavesPermanently = 5f,
            },

            screenSettings = new ScreenSettingsConfig
            {
                widthRatio = 1920,
                heightRatio = 1080
            }
        };
    }
}

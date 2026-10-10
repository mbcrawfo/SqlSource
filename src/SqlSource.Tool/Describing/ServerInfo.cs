namespace SqlSource.Tool.Describing;

/// <summary>
/// What a session knows of its server.
/// </summary>
/// <param name="Version">The server's version, as a sidecar records it.</param>
/// <param name="Driver">The name of the driver, or null.</param>
/// <param name="DriverVersion">The version of the driver, or null.</param>
/// <param name="Settings">The settings of the session that change a description.  Sub-phase 2.6 logs them.</param>
internal sealed record ServerInfo(
    string Version,
    string? Driver,
    string? DriverVersion,
    EquatableArray<ServerSetting> Settings
);

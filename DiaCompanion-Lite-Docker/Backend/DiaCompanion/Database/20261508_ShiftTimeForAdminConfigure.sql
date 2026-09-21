USE DiaCompanion;
GO

-- Giờ bắt đầu ca sáng
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.SystemConfigs
    WHERE [Key] = 'clinic.shift_morning_start'
)
BEGIN
    INSERT INTO dbo.SystemConfigs
    (
        [Key],
        [Value],
        ValueType,
        [Description],
        MinValue,
        MaxValue,
        UpdatedBy,
        UpdatedAt
    )
    VALUES
    (
        'clinic.shift_morning_start',
        '07:00',
        'time',
        N'Giờ bắt đầu ca sáng theo múi giờ cơ sở',
        0.00,
        23.59,
        NULL,
        SYSUTCDATETIME()
    );
END;
GO

-- Giờ bắt đầu ca chiều
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.SystemConfigs
    WHERE [Key] = 'clinic.shift_afternoon_start'
)
BEGIN
    INSERT INTO dbo.SystemConfigs
    (
        [Key],
        [Value],
        ValueType,
        [Description],
        MinValue,
        MaxValue,
        UpdatedBy,
        UpdatedAt
    )
    VALUES
    (
        'clinic.shift_afternoon_start',
        '14:00',
        'time',
        N'Giờ bắt đầu ca chiều theo múi giờ cơ sở',
        0.00,
        23.59,
        NULL,
        SYSUTCDATETIME()
    );
END;
GO

-- Giờ bắt đầu ca đêm
IF NOT EXISTS
(
    SELECT 1
    FROM dbo.SystemConfigs
    WHERE [Key] = 'clinic.shift_night_start'
)
BEGIN
    INSERT INTO dbo.SystemConfigs
    (
        [Key],
        [Value],
        ValueType,
        [Description],
        MinValue,
        MaxValue,
        UpdatedBy,
        UpdatedAt
    )
    VALUES
    (
        'clinic.shift_night_start',
        '18:00',
        'time',
        N'Giờ bắt đầu ca đêm theo múi giờ cơ sở',
        0.00,
        23.59,
        NULL,
        SYSUTCDATETIME()
    );
END;
GO
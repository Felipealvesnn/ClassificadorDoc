-- Script para inserir dados de teste para os gráficos

-- Inserir dados de processamento de lotes (BatchProcessingHistories)
INSERT INTO
    BatchProcessingHistories (
        Id,
        BatchName,
        TotalDocuments,
        SuccessfulDocuments,
        FailedDocuments,
        AverageConfidence,
        StartedAt,
        CompletedAt,
        ProcessingDuration,
        CreatedBy,
        Status
    )
VALUES (
        NEWID (),
        'Lote_001',
        45,
        42,
        3,
        0.89,
        DATEADD (day, -15, GETDATE ()),
        DATEADD (
            day,
            -15,
            DATEADD (minute, 15, GETDATE ())
        ),
        '00:15:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_002',
        52,
        50,
        2,
        0.92,
        DATEADD (day, -14, GETDATE ()),
        DATEADD (
            day,
            -14,
            DATEADD (minute, 18, GETDATE ())
        ),
        '00:18:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_003',
        38,
        36,
        2,
        0.87,
        DATEADD (day, -13, GETDATE ()),
        DATEADD (
            day,
            -13,
            DATEADD (minute, 12, GETDATE ())
        ),
        '00:12:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_004',
        47,
        45,
        2,
        0.91,
        DATEADD (day, -12, GETDATE ()),
        DATEADD (
            day,
            -12,
            DATEADD (minute, 16, GETDATE ())
        ),
        '00:16:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_005',
        63,
        61,
        2,
        0.94,
        DATEADD (day, -11, GETDATE ()),
        DATEADD (
            day,
            -11,
            DATEADD (minute, 22, GETDATE ())
        ),
        '00:22:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_006',
        41,
        39,
        2,
        0.88,
        DATEADD (day, -10, GETDATE ()),
        DATEADD (
            day,
            -10,
            DATEADD (minute, 14, GETDATE ())
        ),
        '00:14:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_007',
        58,
        56,
        2,
        0.93,
        DATEADD (day, -9, GETDATE ()),
        DATEADD (
            day,
            -9,
            DATEADD (minute, 20, GETDATE ())
        ),
        '00:20:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_008',
        69,
        67,
        2,
        0.95,
        DATEADD (day, -8, GETDATE ()),
        DATEADD (
            day,
            -8,
            DATEADD (minute, 25, GETDATE ())
        ),
        '00:25:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_009',
        72,
        70,
        2,
        0.96,
        DATEADD (day, -7, GETDATE ()),
        DATEADD (
            day,
            -7,
            DATEADD (minute, 28, GETDATE ())
        ),
        '00:28:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_010',
        55,
        53,
        2,
        0.90,
        DATEADD (day, -6, GETDATE ()),
        DATEADD (
            day,
            -6,
            DATEADD (minute, 19, GETDATE ())
        ),
        '00:19:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_011',
        48,
        46,
        2,
        0.86,
        DATEADD (day, -5, GETDATE ()),
        DATEADD (
            day,
            -5,
            DATEADD (minute, 17, GETDATE ())
        ),
        '00:17:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_012',
        61,
        59,
        2,
        0.92,
        DATEADD (day, -4, GETDATE ()),
        DATEADD (
            day,
            -4,
            DATEADD (minute, 21, GETDATE ())
        ),
        '00:21:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_013',
        67,
        65,
        2,
        0.94,
        DATEADD (day, -3, GETDATE ()),
        DATEADD (
            day,
            -3,
            DATEADD (minute, 24, GETDATE ())
        ),
        '00:24:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_014',
        59,
        57,
        2,
        0.91,
        DATEADD (day, -2, GETDATE ()),
        DATEADD (
            day,
            -2,
            DATEADD (minute, 20, GETDATE ())
        ),
        '00:20:00',
        'admin@classificador.com',
        'Completed'
    ),
    (
        NEWID (),
        'Lote_015',
        44,
        42,
        2,
        0.87,
        DATEADD (day, -1, GETDATE ()),
        DATEADD (
            day,
            -1,
            DATEADD (minute, 15, GETDATE ())
        ),
        '00:15:00',
        'admin@classificador.com',
        'Completed'
    );

-- Inserir dados de produtividade de usuários (UserProductivities)
DECLARE @AdminUserId NVARCHAR (450);

DECLARE @UserUserId NVARCHAR (450);

DECLARE @ClassifierUserId NVARCHAR (450);

SELECT @AdminUserId = Id
FROM AspNetUsers
WHERE
    Email = 'admin@classificador.com';

SELECT @UserUserId = Id
FROM AspNetUsers
WHERE
    Email = 'usuario@classificador.com';

SELECT @ClassifierUserId = Id
FROM AspNetUsers
WHERE
    Email = 'classificador@classificador.com';

INSERT INTO
    UserProductivities (
        Id,
        UserId,
        Date,
        DocumentsProcessed,
        AverageProcessingTime,
        LoginCount,
        ActiveTime,
        AccuracyRate
    )
VALUES (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -15, GETDATE ()) AS DATE
        ),
        15,
        180,
        3,
        420,
        0.92
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -15, GETDATE ()) AS DATE
        ),
        12,
        220,
        2,
        380,
        0.88
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -15, GETDATE ()) AS DATE
        ),
        18,
        160,
        4,
        480,
        0.95
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -14, GETDATE ()) AS DATE
        ),
        18,
        175,
        3,
        440,
        0.94
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -14, GETDATE ()) AS DATE
        ),
        14,
        210,
        2,
        390,
        0.89
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -14, GETDATE ()) AS DATE
        ),
        20,
        155,
        4,
        500,
        0.96
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -13, GETDATE ()) AS DATE
        ),
        12,
        190,
        2,
        360,
        0.90
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -13, GETDATE ()) AS DATE
        ),
        10,
        235,
        2,
        320,
        0.85
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -13, GETDATE ()) AS DATE
        ),
        16,
        165,
        3,
        450,
        0.93
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -12, GETDATE ()) AS DATE
        ),
        16,
        185,
        3,
        400,
        0.91
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -12, GETDATE ()) AS DATE
        ),
        13,
        225,
        2,
        370,
        0.87
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -12, GETDATE ()) AS DATE
        ),
        18,
        158,
        4,
        470,
        0.94
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -11, GETDATE ()) AS DATE
        ),
        20,
        170,
        4,
        480,
        0.95
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -11, GETDATE ()) AS DATE
        ),
        16,
        200,
        3,
        420,
        0.91
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -11, GETDATE ()) AS DATE
        ),
        22,
        150,
        5,
        520,
        0.97
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -10, GETDATE ()) AS DATE
        ),
        14,
        195,
        2,
        380,
        0.89
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -10, GETDATE ()) AS DATE
        ),
        11,
        240,
        2,
        340,
        0.86
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -10, GETDATE ()) AS DATE
        ),
        16,
        170,
        3,
        430,
        0.92
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -9, GETDATE ()) AS DATE
        ),
        19,
        180,
        3,
        450,
        0.93
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -9, GETDATE ()) AS DATE
        ),
        15,
        215,
        3,
        390,
        0.88
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -9, GETDATE ()) AS DATE
        ),
        21,
        155,
        4,
        490,
        0.95
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -8, GETDATE ()) AS DATE
        ),
        22,
        165,
        4,
        500,
        0.96
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -8, GETDATE ()) AS DATE
        ),
        18,
        195,
        3,
        430,
        0.90
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -8, GETDATE ()) AS DATE
        ),
        24,
        145,
        5,
        540,
        0.98
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -7, GETDATE ()) AS DATE
        ),
        24,
        160,
        4,
        520,
        0.97
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -7, GETDATE ()) AS DATE
        ),
        20,
        190,
        3,
        450,
        0.91
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -7, GETDATE ()) AS DATE
        ),
        26,
        140,
        5,
        560,
        0.99
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -6, GETDATE ()) AS DATE
        ),
        18,
        175,
        3,
        440,
        0.92
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -6, GETDATE ()) AS DATE
        ),
        15,
        210,
        2,
        400,
        0.87
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -6, GETDATE ()) AS DATE
        ),
        20,
        160,
        4,
        480,
        0.94
    );

-- Dados adicionais para os últimos 5 dias
INSERT INTO
    UserProductivities (
        Id,
        UserId,
        Date,
        DocumentsProcessed,
        AverageProcessingTime,
        LoginCount,
        ActiveTime,
        AccuracyRate
    )
VALUES (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -5, GETDATE ()) AS DATE
        ),
        16,
        185,
        3,
        410,
        0.90
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -5, GETDATE ()) AS DATE
        ),
        12,
        230,
        2,
        360,
        0.85
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -5, GETDATE ()) AS DATE
        ),
        18,
        165,
        4,
        460,
        0.93
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -4, GETDATE ()) AS DATE
        ),
        20,
        170,
        4,
        470,
        0.94
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -4, GETDATE ()) AS DATE
        ),
        16,
        205,
        3,
        420,
        0.89
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -4, GETDATE ()) AS DATE
        ),
        22,
        150,
        5,
        510,
        0.96
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -3, GETDATE ()) AS DATE
        ),
        22,
        165,
        4,
        490,
        0.95
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -3, GETDATE ()) AS DATE
        ),
        18,
        195,
        3,
        440,
        0.90
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -3, GETDATE ()) AS DATE
        ),
        24,
        145,
        5,
        530,
        0.97
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -2, GETDATE ()) AS DATE
        ),
        19,
        180,
        3,
        460,
        0.92
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -2, GETDATE ()) AS DATE
        ),
        15,
        220,
        2,
        380,
        0.87
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -2, GETDATE ()) AS DATE
        ),
        21,
        155,
        4,
        490,
        0.94
    ),
    (
        NEWID (),
        @AdminUserId,
        CAST(
            DATEADD (day, -1, GETDATE ()) AS DATE
        ),
        14,
        195,
        2,
        390,
        0.88
    ),
    (
        NEWID (),
        @UserUserId,
        CAST(
            DATEADD (day, -1, GETDATE ()) AS DATE
        ),
        11,
        240,
        2,
        340,
        0.84
    ),
    (
        NEWID (),
        @ClassifierUserId,
        CAST(
            DATEADD (day, -1, GETDATE ()) AS DATE
        ),
        17,
        170,
        3,
        440,
        0.91
    );

PRINT 'Dados de teste inseridos com sucesso!';
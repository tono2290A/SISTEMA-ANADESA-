-- Asistencia por grupo: marca presente/ausente por miembro y fecha
-- desde el detalle del grupo (panel de miembros en Actividades > Grupos).
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID('dbo.asistencia_grupo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.asistencia_grupo (
        id_asistencia_grupo INT IDENTITY(1, 1) PRIMARY KEY,
        id_grupo INT NOT NULL,
        id_participante INT NOT NULL,
        fecha DATE NOT NULL CONSTRAINT DF_asistencia_grupo_fecha DEFAULT CAST(GETDATE() AS DATE),
        presente BIT NOT NULL,
        fecha_registro DATETIME NOT NULL CONSTRAINT DF_asistencia_grupo_fecha_registro DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX UX_asistencia_grupo ON dbo.asistencia_grupo (id_grupo, id_participante, fecha);
END;
GO

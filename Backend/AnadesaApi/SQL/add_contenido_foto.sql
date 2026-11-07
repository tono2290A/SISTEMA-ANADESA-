-- Fotos en la BD: los bytes del contenido sobreviven redeploys de Render
-- (el disco del contenedor es efimero). Archivo en disco queda como respaldo.
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH('dbo.fotografias_participante', 'contenido') IS NULL
    ALTER TABLE dbo.fotografias_participante ADD contenido VARBINARY(MAX) NULL;
GO

-- Permiso individual de grupos: el administrador otorga (switch por usuario)
-- en la lista de usuarios. Nuevos usuarios nacen con el permiso apagado.
SET QUOTED_IDENTIFIER ON;
GO
IF COL_LENGTH('dbo.usuarios', 'puede_crear_grupos') IS NULL
    ALTER TABLE dbo.usuarios ADD puede_crear_grupos BIT NOT NULL DEFAULT 0;
GO

/*
    Standardize ParkingSlots.Status:
      Available, Occupied, Assigned, Maintenance, Locked

    Reserved was previously used for monthly fixed slots. Those rows are
    migrated to Assigned before the CHECK constraint is replaced.
*/

USE [ParkingDB];
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE dbo.ParkingSlots
    SET Status = 'Assigned'
    WHERE Status = 'Reserved';

    DECLARE @DropCheckConstraints nvarchar(max) = N'';

    SELECT @DropCheckConstraints +=
        N'ALTER TABLE dbo.ParkingSlots DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';'
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.ParkingSlots')
      AND cc.definition LIKE N'%Status%';

    IF @DropCheckConstraints <> N''
    BEGIN
        EXEC sys.sp_executesql @DropCheckConstraints;
    END;

    ALTER TABLE dbo.ParkingSlots WITH CHECK
    ADD CONSTRAINT CK_ParkingSlots_Status
    CHECK (Status IN ('Available', 'Occupied', 'Assigned', 'Maintenance', 'Locked'));

    ALTER TABLE dbo.ParkingSlots
    CHECK CONSTRAINT CK_ParkingSlots_Status;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

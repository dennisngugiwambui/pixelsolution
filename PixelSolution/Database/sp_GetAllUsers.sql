-- Stored Procedure: sp_GetAllUsers
-- Description: Retrieves all users with their department information and calculated fields

USE PixelSolutionDb;
GO

-- Drop if exists
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetAllUsers')
    DROP PROCEDURE sp_GetAllUsers;
GO

CREATE PROCEDURE sp_GetAllUsers
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.FirstName,
        u.LastName,
        u.FirstName + ' ' + u.LastName AS FullName,
        u.Email,
        u.Phone,
        u.UserType,
        u.Status,
        u.Privileges,
        u.DepartmentId,
        u.CreatedAt,
        u.UpdatedAt,
        d.Name AS DepartmentName,
        ISNULL(d.Name, 'No Department') AS DepartmentNames,
        
        -- Calculate sales statistics
        ISNULL(sales_stats.TotalSales, 0) AS TotalSales,
        ISNULL(sales_stats.TotalSalesAmount, 0.00) AS TotalSalesAmount
        
    FROM Users u
    LEFT JOIN Departments d ON u.DepartmentId = d.DepartmentId
    LEFT JOIN (
        SELECT 
            s.UserId,
            COUNT(*) AS TotalSales,
            SUM(s.TotalAmount) AS TotalSalesAmount
        FROM Sales s
        WHERE s.Status = 'Completed'
        GROUP BY s.UserId
    ) sales_stats ON u.UserId = sales_stats.UserId
    
    ORDER BY u.FirstName, u.LastName;
END;
GO

-- Grant execute permissions
GRANT EXECUTE ON sp_GetAllUsers TO PUBLIC;
GO

PRINT 'Stored procedure sp_GetAllUsers created successfully!';

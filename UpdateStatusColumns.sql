-- Update existing Product records to have default Status value
UPDATE Products 
SET Status = 'Active' 
WHERE Status IS NULL OR Status = '';

-- Update existing Category records to have default Status value  
UPDATE Categories 
SET Status = 'Active' 
WHERE Status IS NULL OR Status = '';

-- Verify the updates
SELECT 'Products' as TableName, COUNT(*) as TotalRecords, 
       SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveRecords
FROM Products
UNION ALL
SELECT 'Categories' as TableName, COUNT(*) as TotalRecords,
       SUM(CASE WHEN Status = 'Active' THEN 1 ELSE 0 END) as ActiveRecords  
FROM Categories;

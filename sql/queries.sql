-- 1. Catalog search with category and price filters (parameterized values)
SELECT "Id", "Sku", "Name", "Category", "Price", "Inventory"
FROM "Products"
WHERE "IsActive" = TRUE
  AND (@category IS NULL OR "Category" = @category)
  AND (@max_price IS NULL OR "Price" <= @max_price)
ORDER BY "Price", "Name"
LIMIT @page_size OFFSET @offset;

-- 2. Best sellers by units ordered
SELECT p."Sku", p."Name", SUM(oi."Quantity") AS units_sold,
       SUM(oi."Quantity" * oi."UnitPrice") AS gross_sales
FROM "OrderItems" oi
JOIN "Products" p ON p."Id" = oi."ProductId"
JOIN "Orders" o ON o."Id" = oi."OrderId"
WHERE o."Status" = 'Paid'
GROUP BY p."Sku", p."Name"
ORDER BY units_sold DESC
LIMIT 20;

-- 3. Customer lifetime value and order count
SELECT c."Email", c."FirstName", COUNT(o."Id") AS order_count,
       COALESCE(SUM(o."Total"), 0) AS lifetime_value
FROM "Customers" c
LEFT JOIN "Orders" o ON o."CustomerId" = c."Id" AND o."Status" = 'Paid'
GROUP BY c."Id", c."Email", c."FirstName"
ORDER BY lifetime_value DESC;

-- 4. Reorder / low inventory report
SELECT "Sku", "Name", "Inventory"
FROM "Products"
WHERE "IsActive" = TRUE AND "Inventory" <= 10
ORDER BY "Inventory", "Name";

-- 5. Daily revenue for the previous 30 days
SELECT date_trunc('day', "CreatedAt")::date AS order_day,
       COUNT(*) AS orders, SUM("Subtotal") AS merchandise,
       SUM("Shipping") AS shipping, SUM("Tax") AS tax, SUM("Total") AS revenue
FROM "Orders"
WHERE "Status" = 'Paid' AND "CreatedAt" >= now() - interval '30 days'
GROUP BY order_day ORDER BY order_day DESC;

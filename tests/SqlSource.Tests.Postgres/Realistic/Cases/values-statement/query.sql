-- name: ListShippingOptions
VALUES
    ('standard', 'Standard delivery', 4.95, NULL),
    ('express', 'Next-day delivery', 14.50, 1),
    ('pickup', 'Collect in store', 0, NULL)
ORDER BY 3;

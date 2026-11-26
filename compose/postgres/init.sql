-- Local only: the same two database users CDP creates for the service.
--   waste_submit_reg_packaging_api_ddl  owns the schema; Liquibase runs as this user.
--   waste_submit_reg_packaging_api      the app; can read and write data but not change the schema.
-- This means a change that only works with DDL rights (for example EF migrating at startup) fails
-- locally too, not just in CDP.

CREATE ROLE waste_submit_reg_packaging_api_ddl LOGIN;
CREATE ROLE waste_submit_reg_packaging_api LOGIN;

GRANT CONNECT ON DATABASE waste_submit_reg_packaging_api
    TO waste_submit_reg_packaging_api_ddl, waste_submit_reg_packaging_api;

GRANT USAGE, CREATE ON SCHEMA public TO waste_submit_reg_packaging_api_ddl;
GRANT USAGE ON SCHEMA public TO waste_submit_reg_packaging_api;

-- Tables the DDL user creates later are readable and writable by the app.
ALTER DEFAULT PRIVILEGES FOR ROLE waste_submit_reg_packaging_api_ddl IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO waste_submit_reg_packaging_api;
ALTER DEFAULT PRIVILEGES FOR ROLE waste_submit_reg_packaging_api_ddl IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO waste_submit_reg_packaging_api;

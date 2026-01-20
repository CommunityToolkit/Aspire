namespace CommunityToolkit.Aspire.Hosting.Supabase.Helpers;

/// <summary>
/// Helper class for generating SQL scripts and configuration files for Supabase.
/// </summary>
internal static class SupabaseSqlGenerator
{
    #region SQL Escaping

    /// <summary>
    /// Escapes a string value for safe use in SQL literals.
    /// </summary>
    public static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    /// <summary>
    /// Escapes a string value for safe use in SQL strings with backslash support.
    /// </summary>
    public static string EscapeSqlString(string value) => value.Replace("'", "''").Replace("\\", "\\\\");

    #endregion

    #region Init SQL

    /// <summary>
    /// Writes the main initialization SQL script for Supabase.
    /// Creates roles, schemas, extensions, storage tables, and triggers.
    /// </summary>
    public static void WriteInitSql(string initDir, string password)
    {
        var pw = EscapeSqlLiteral(password);

        var sql = $"""
-- ============================================
-- SUPABASE INITIALIZATION SCRIPT
-- ============================================

-- 1. Rollen erstellen
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'supabase_admin') THEN
    CREATE ROLE supabase_admin LOGIN PASSWORD '{pw}' SUPERUSER CREATEDB CREATEROLE REPLICATION BYPASSRLS;
  ELSE
    ALTER ROLE supabase_admin WITH PASSWORD '{pw}';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
    CREATE ROLE anon NOLOGIN NOINHERIT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
    CREATE ROLE authenticated NOLOGIN NOINHERIT;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
    CREATE ROLE service_role NOLOGIN NOINHERIT BYPASSRLS;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticator') THEN
    CREATE ROLE authenticator LOGIN PASSWORD '{pw}' NOINHERIT;
  ELSE
    ALTER ROLE authenticator WITH PASSWORD '{pw}';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'supabase_auth_admin') THEN
    CREATE ROLE supabase_auth_admin LOGIN PASSWORD '{pw}' NOINHERIT CREATEROLE;
  ELSE
    ALTER ROLE supabase_auth_admin WITH PASSWORD '{pw}';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'supabase_storage_admin') THEN
    CREATE ROLE supabase_storage_admin LOGIN PASSWORD '{pw}' NOINHERIT BYPASSRLS;
  ELSE
    ALTER ROLE supabase_storage_admin WITH PASSWORD '{pw}' BYPASSRLS;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'dashboard_user') THEN
    CREATE ROLE dashboard_user NOLOGIN;
  END IF;
END
$$;

-- 2. Rollen-Mitgliedschaften
GRANT anon, authenticated, service_role TO authenticator;
GRANT anon, authenticated, service_role TO supabase_storage_admin;
GRANT supabase_auth_admin TO supabase_admin;
GRANT supabase_storage_admin TO supabase_admin;
GRANT supabase_auth_admin TO postgres;
GRANT ALL ON DATABASE postgres TO supabase_admin;

-- 3. Schemata erstellen
CREATE SCHEMA IF NOT EXISTS auth AUTHORIZATION supabase_auth_admin;
CREATE SCHEMA IF NOT EXISTS storage AUTHORIZATION supabase_storage_admin;
CREATE SCHEMA IF NOT EXISTS extensions AUTHORIZATION supabase_admin;
CREATE SCHEMA IF NOT EXISTS graphql_public;

-- 4. Extensions installieren
CREATE EXTENSION IF NOT EXISTS "uuid-ossp" SCHEMA extensions;
CREATE EXTENSION IF NOT EXISTS pgcrypto SCHEMA extensions;

-- 5. Grants für public Schema
GRANT USAGE ON SCHEMA public TO anon, authenticated, service_role;
GRANT ALL ON SCHEMA public TO supabase_admin, supabase_auth_admin, supabase_storage_admin;
GRANT CREATE ON SCHEMA public TO supabase_auth_admin, supabase_storage_admin;
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon, authenticated, service_role, supabase_auth_admin, supabase_storage_admin;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO anon, authenticated, service_role, supabase_auth_admin, supabase_storage_admin;
GRANT ALL ON ALL FUNCTIONS IN SCHEMA public TO anon, authenticated, service_role;

ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO anon, authenticated, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO anon, authenticated, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO anon, authenticated, service_role;

-- 6. Grants für extensions Schema
GRANT USAGE ON SCHEMA extensions TO anon, authenticated, service_role, supabase_admin;
GRANT ALL ON ALL FUNCTIONS IN SCHEMA extensions TO anon, authenticated, service_role;

-- 7. Auth Enums erstellen
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname='auth' AND t.typname='factor_type') THEN
    CREATE TYPE auth.factor_type AS ENUM ('totp', 'webauthn');
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname='auth' AND t.typname='factor_status') THEN
    CREATE TYPE auth.factor_status AS ENUM ('unverified', 'verified');
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname='auth' AND t.typname='aal_level') THEN
    CREATE TYPE auth.aal_level AS ENUM ('aal1', 'aal2', 'aal3');
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname='auth' AND t.typname='code_challenge_method') THEN
    CREATE TYPE auth.code_challenge_method AS ENUM ('s256', 'plain');
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname='auth' AND t.typname='one_time_token_type') THEN
    CREATE TYPE auth.one_time_token_type AS ENUM ('confirmation_token', 'reauthentication_token', 'recovery_token', 'email_change_token_new', 'email_change_token_current', 'phone_change_token');
  END IF;
END
$$;

ALTER TYPE auth.factor_type OWNER TO supabase_auth_admin;
ALTER TYPE auth.factor_status OWNER TO supabase_auth_admin;
ALTER TYPE auth.aal_level OWNER TO supabase_auth_admin;
ALTER TYPE auth.code_challenge_method OWNER TO supabase_auth_admin;
ALTER TYPE auth.one_time_token_type OWNER TO supabase_auth_admin;

-- 8. Grants für auth Schema (GoTrue erstellt Tabellen selbst via Migrationen)
GRANT USAGE ON SCHEMA auth TO supabase_auth_admin, supabase_admin, service_role, postgres;
GRANT ALL ON ALL TABLES IN SCHEMA auth TO supabase_auth_admin, supabase_admin;
GRANT ALL ON ALL SEQUENCES IN SCHEMA auth TO supabase_auth_admin, supabase_admin;

-- 9. Grants für storage Schema
GRANT USAGE ON SCHEMA storage TO supabase_storage_admin, supabase_admin, authenticated, anon, service_role;
GRANT ALL ON ALL TABLES IN SCHEMA storage TO supabase_storage_admin, supabase_admin, service_role;
GRANT SELECT ON ALL TABLES IN SCHEMA storage TO authenticated, anon;
GRANT ALL ON ALL SEQUENCES IN SCHEMA storage TO supabase_storage_admin, supabase_admin, service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA storage GRANT ALL ON TABLES TO service_role;
ALTER DEFAULT PRIVILEGES IN SCHEMA storage GRANT ALL ON SEQUENCES TO service_role;

-- 10. Storage Tabellen erstellen
CREATE TABLE IF NOT EXISTS storage.buckets (
    id text NOT NULL PRIMARY KEY,
    name text NOT NULL UNIQUE,
    owner uuid,
    created_at timestamptz DEFAULT NOW() NOT NULL,
    updated_at timestamptz DEFAULT NOW() NOT NULL,
    public boolean DEFAULT false,
    avif_autodetection boolean DEFAULT false,
    file_size_limit bigint,
    allowed_mime_types text[],
    owner_id text
);
ALTER TABLE storage.buckets OWNER TO supabase_storage_admin;

CREATE TABLE IF NOT EXISTS storage.objects (
    id uuid DEFAULT extensions.uuid_generate_v4() NOT NULL PRIMARY KEY,
    bucket_id text REFERENCES storage.buckets(id),
    name text,
    owner uuid,
    created_at timestamptz DEFAULT NOW(),
    updated_at timestamptz DEFAULT NOW(),
    last_accessed_at timestamptz DEFAULT NOW(),
    metadata jsonb,
    path_tokens text[] GENERATED ALWAYS AS (string_to_array(name, '/')) STORED,
    version text,
    owner_id text
);
ALTER TABLE storage.objects OWNER TO supabase_storage_admin;

CREATE TABLE IF NOT EXISTS storage.s3_multipart_uploads (
    id text NOT NULL PRIMARY KEY,
    in_progress_size bigint DEFAULT 0 NOT NULL,
    upload_signature text NOT NULL,
    bucket_id text NOT NULL REFERENCES storage.buckets(id),
    key text NOT NULL,
    version text NOT NULL,
    owner_id text,
    created_at timestamptz DEFAULT NOW() NOT NULL
);
ALTER TABLE storage.s3_multipart_uploads OWNER TO supabase_storage_admin;

CREATE TABLE IF NOT EXISTS storage.s3_multipart_uploads_parts (
    id uuid DEFAULT extensions.uuid_generate_v4() NOT NULL PRIMARY KEY,
    upload_id text NOT NULL REFERENCES storage.s3_multipart_uploads(id) ON DELETE CASCADE,
    size bigint DEFAULT 0 NOT NULL,
    part_number integer NOT NULL,
    bucket_id text NOT NULL REFERENCES storage.buckets(id),
    key text NOT NULL,
    etag text NOT NULL,
    owner_id text,
    version text NOT NULL,
    created_at timestamptz DEFAULT NOW() NOT NULL
);
ALTER TABLE storage.s3_multipart_uploads_parts OWNER TO supabase_storage_admin;

CREATE TABLE IF NOT EXISTS storage.migrations (
    id integer NOT NULL PRIMARY KEY,
    name varchar(100) NOT NULL UNIQUE,
    hash varchar(40) NOT NULL,
    executed_at timestamp DEFAULT CURRENT_TIMESTAMP
);
ALTER TABLE storage.migrations OWNER TO supabase_storage_admin;

CREATE INDEX IF NOT EXISTS objects_bucket_id_name_idx ON storage.objects (bucket_id, name);
CREATE INDEX IF NOT EXISTS objects_owner_idx ON storage.objects (owner);

-- 11. RLS für Storage aktivieren
ALTER TABLE storage.buckets ENABLE ROW LEVEL SECURITY;
ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Public buckets are viewable by everyone" ON storage.buckets;
CREATE POLICY "Public buckets are viewable by everyone"
    ON storage.buckets FOR SELECT USING (public = true);

DROP POLICY IF EXISTS "Objects in public buckets are viewable by everyone" ON storage.objects;
CREATE POLICY "Objects in public buckets are viewable by everyone"
    ON storage.objects FOR SELECT
    USING (bucket_id IN (SELECT id FROM storage.buckets WHERE public = true));

DROP POLICY IF EXISTS "Service role has full access to buckets" ON storage.buckets;
CREATE POLICY "Service role has full access to buckets"
    ON storage.buckets FOR ALL TO service_role
    USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Service role has full access to objects" ON storage.objects;
CREATE POLICY "Service role has full access to objects"
    ON storage.objects FOR ALL TO service_role
    USING (true) WITH CHECK (true);

-- Dev-Mode: Allow all für Storage (anon und authenticated)
DROP POLICY IF EXISTS "Allow all for development" ON storage.buckets;
CREATE POLICY "Allow all for development"
    ON storage.buckets FOR ALL
    USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Allow all for development" ON storage.objects;
CREATE POLICY "Allow all for development"
    ON storage.objects FOR ALL
    USING (true) WITH CHECK (true);

GRANT ALL ON storage.buckets TO anon, authenticated;
GRANT ALL ON storage.objects TO anon, authenticated;

-- 12. Search Path für PostgREST
ALTER DATABASE postgres SET search_path TO public, extensions;

-- 13. Schema Reload Trigger
CREATE OR REPLACE FUNCTION extensions.notify_api_restart()
RETURNS event_trigger LANGUAGE plpgsql AS $$
BEGIN
    NOTIFY pgrst, 'reload schema';
END;
$$;

DROP EVENT TRIGGER IF EXISTS api_restart;
CREATE EVENT TRIGGER api_restart ON ddl_command_end
    EXECUTE FUNCTION extensions.notify_api_restart();

-- 14. Auto-Create Profile & Role Trigger für neue User
-- WICHTIG: Mit Exception-Handling damit User-Erstellung NIEMALS blockiert wird
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER SET search_path = public, extensions
AS $$
BEGIN
    -- Profil erstellen (mit Exception-Handling)
    BEGIN
        IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'profiles') THEN
            IF NOT EXISTS (SELECT 1 FROM public.profiles WHERE user_id = NEW.id) THEN
                INSERT INTO public.profiles (user_id, email, display_name, is_disabled, created_at, updated_at)
                VALUES (
                    NEW.id,
                    NEW.email,
                    COALESCE(NEW.raw_user_meta_data->>'display_name', NEW.email),
                    false,
                    NOW(),
                    NOW()
                );
            END IF;
        END IF;
    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING '[handle_new_user] Profil-Erstellung fehlgeschlagen für %: %', NEW.email, SQLERRM;
    END;

    -- Admin-Rolle erstellen (mit Exception-Handling)
    BEGIN
        IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'user_roles') THEN
            IF NOT EXISTS (SELECT 1 FROM public.user_roles WHERE user_id = NEW.id) THEN
                INSERT INTO public.user_roles (user_id, role, created_at)
                VALUES (
                    NEW.id,
                    'admin',
                    NOW()
                );
            END IF;
        END IF;
    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING '[handle_new_user] Rollen-Erstellung fehlgeschlagen für %: %', NEW.email, SQLERRM;
    END;

    -- IMMER NEW zurückgeben, damit User-Erstellung NICHT blockiert wird
    RETURN NEW;
END;
$$;

-- Trigger erstellen (falls auth.users existiert)
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users') THEN
        DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
        CREATE TRIGGER on_auth_user_created
            AFTER INSERT ON auth.users
            FOR EACH ROW
            EXECUTE FUNCTION public.handle_new_user();
    END IF;
END;
$$;
""";
        File.WriteAllText(Path.Combine(initDir, "00_init.sql"), sql);
    }

    #endregion

    #region Post-Init SQL

    /// <summary>
    /// Writes the post-initialization SQL script that runs after GoTrue starts.
    /// Creates triggers for new users and fills in profiles for existing users.
    /// </summary>
    public static void WritePostInitSql(string path)
    {
        var sql = """
-- ============================================
-- POST-INIT: User-Erstellung und Profile
-- ============================================

-- Kurz warten damit GoTrue die Tabellen erstellen kann
SELECT pg_sleep(2);

-- Trigger erstellen (auth.users existiert jetzt)
DO $$
BEGIN
    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users') THEN
        IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'on_auth_user_created') THEN
            CREATE TRIGGER on_auth_user_created
                AFTER INSERT ON auth.users
                FOR EACH ROW
                EXECUTE FUNCTION public.handle_new_user();
            RAISE NOTICE '[Post-Init] Trigger on_auth_user_created erstellt';
        ELSE
            RAISE NOTICE '[Post-Init] Trigger on_auth_user_created existiert bereits';
        END IF;
    ELSE
        RAISE NOTICE '[Post-Init] auth.users existiert noch nicht';
    END IF;
END;
$$;

-- Erstelle Profile für weitere existierende User ohne Profil (mit Exception-Handling)
DO $$
DECLARE
    inserted_count integer;
BEGIN
    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'profiles')
       AND EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users')
    THEN
        BEGIN
            INSERT INTO public.profiles (user_id, email, display_name, is_disabled, created_at, updated_at)
            SELECT
                u.id,
                u.email,
                COALESCE(u.raw_user_meta_data->>'display_name', u.email),
                false,
                NOW(),
                NOW()
            FROM auth.users u
            WHERE NOT EXISTS (SELECT 1 FROM public.profiles p WHERE p.user_id = u.id);

            GET DIAGNOSTICS inserted_count = ROW_COUNT;
            IF inserted_count > 0 THEN
                RAISE NOTICE '[Post-Init] % weitere Profile erstellt', inserted_count;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Profil-Erstellung fehlgeschlagen: %', SQLERRM;
        END;
    END IF;
END;
$$;

-- Erstelle Admin-Rollen für User ohne Rolle (mit Exception-Handling)
DO $$
DECLARE
    inserted_count integer;
BEGIN
    IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'user_roles')
       AND EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users')
    THEN
        BEGIN
            INSERT INTO public.user_roles (user_id, role, created_at)
            SELECT
                u.id,
                'admin',
                NOW()
            FROM auth.users u
            WHERE NOT EXISTS (SELECT 1 FROM public.user_roles r WHERE r.user_id = u.id);

            GET DIAGNOSTICS inserted_count = ROW_COUNT;
            IF inserted_count > 0 THEN
                RAISE NOTICE '[Post-Init] % Admin-Rollen erstellt', inserted_count;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Rollen-Erstellung fehlgeschlagen: %', SQLERRM;
        END;
    END IF;
END;
$$;

SELECT 'Post-Init abgeschlossen' as status;
""";
        File.WriteAllText(path, sql);
    }

    #endregion

    #region Post-Init Shell Script

    /// <summary>
    /// Writes the post-initialization shell script that waits for the database
    /// and executes post_init.sql, migrations.sql, and users.sql.
    /// </summary>
    public static void WritePostInitScript(string path, string dbHost, string password)
    {
        var script = $"""
#!/bin/bash
# Post-Init Script mit Retry-Logik

export PGPASSWORD='{password}'
DB_HOST='{dbHost}'
MAX_RETRIES=30
RETRY_INTERVAL=2

echo "[Post-Init] Warte auf Datenbankverbindung..."

for i in $(seq 1 $MAX_RETRIES); do
    if pg_isready -h $DB_HOST -U postgres -q; then
        echo "[Post-Init] Datenbank ist bereit (Versuch $i)"
        break
    fi
    echo "[Post-Init] Warte auf Datenbank... (Versuch $i/$MAX_RETRIES)"
    sleep $RETRY_INTERVAL
done

# Zusätzliche Wartezeit damit GoTrue die Tabellen erstellen kann
echo "[Post-Init] Warte 10 Sekunden für GoTrue Migrationen..."
sleep 10

echo "[Post-Init] Führe Basis-SQL aus..."
psql -h $DB_HOST -U postgres -d postgres -f /scripts/post_init.sql

# Migrations ausführen falls vorhanden (muss NACH GoTrue laufen wegen auth.users)
if [ -f /scripts/migrations.sql ]; then
    echo "[Post-Init] Führe Migrations aus..."
    psql -h $DB_HOST -U postgres -d postgres -f /scripts/migrations.sql
    if [ $? -eq 0 ]; then
        echo "[Post-Init] Migrations erfolgreich"
    else
        echo "[Post-Init] WARNUNG: Migrations hatten Fehler"
    fi
fi

# User-SQL ausführen falls vorhanden
if [ -f /scripts/users.sql ]; then
    echo "[Post-Init] Führe User-SQL aus..."
    psql -h $DB_HOST -U postgres -d postgres -f /scripts/users.sql
fi

echo "[Post-Init] Erfolgreich abgeschlossen"
""";
        File.WriteAllText(path, script.Replace("\r\n", "\n")); // Unix line endings
    }

    #endregion

    #region User SQL

    /// <summary>
    /// Appends SQL to create a user with profile and admin role.
    /// </summary>
    public static void AppendUserSql(string path, string email, string password, string displayName)
    {
        var escapedEmail = EscapeSqlLiteral(email);
        var escapedDisplayName = EscapeSqlLiteral(displayName);
        var escapedPassword = EscapeSqlLiteral(password);

        var appMetaData = @"{""provider"": ""email"", ""providers"": [""email""]}";
        var userMetaData = @"{""display_name"": """ + escapedDisplayName + @"""}";

        var sql = $"""
-- User: {email}
DO $$
DECLARE
    new_user_id uuid;
    hashed_password text;
BEGIN
    -- Prüfe ob User bereits existiert
    SELECT id INTO new_user_id FROM auth.users WHERE email = '{escapedEmail}';

    IF new_user_id IS NULL THEN
        -- Passwort hashen
        hashed_password := extensions.crypt('{escapedPassword}', extensions.gen_salt('bf', 10));

        -- User in auth.users erstellen
        INSERT INTO auth.users (
            instance_id, id, aud, role, email, encrypted_password,
            email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
            created_at, updated_at, confirmation_token, email_change,
            email_change_token_new, recovery_token
        ) VALUES (
            '00000000-0000-0000-0000-000000000000',
            extensions.uuid_generate_v4(),
            'authenticated', 'authenticated', '{escapedEmail}', hashed_password,
            NOW(), '{appMetaData}'::jsonb, '{userMetaData}'::jsonb,
            NOW(), NOW(), '', '', '', ''
        )
        RETURNING id INTO new_user_id;

        RAISE NOTICE '[Post-Init] User erstellt: {escapedEmail} (ID: %)', new_user_id;

        -- Profil erstellen (mit Exception-Handling)
        BEGIN
            IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'profiles') THEN
                IF NOT EXISTS (SELECT 1 FROM public.profiles WHERE user_id = new_user_id) THEN
                    INSERT INTO public.profiles (user_id, email, display_name, is_disabled, created_at, updated_at)
                    VALUES (new_user_id, '{escapedEmail}', '{escapedDisplayName}', false, NOW(), NOW());
                END IF;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Profil-Erstellung fehlgeschlagen für {escapedEmail}: %', SQLERRM;
        END;

        -- Admin-Rolle erstellen (mit Exception-Handling)
        BEGIN
            IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'user_roles') THEN
                IF NOT EXISTS (SELECT 1 FROM public.user_roles WHERE user_id = new_user_id) THEN
                    INSERT INTO public.user_roles (user_id, role, created_at)
                    VALUES (new_user_id, 'admin', NOW());
                END IF;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Rollen-Erstellung fehlgeschlagen für {escapedEmail}: %', SQLERRM;
        END;
    ELSE
        RAISE NOTICE '[Post-Init] User existiert bereits: {escapedEmail}';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE WARNING '[Post-Init] User-Erstellung komplett fehlgeschlagen für {escapedEmail}: %', SQLERRM;
END;
$$;

""";
        File.AppendAllText(path, sql);
    }

    #endregion

    #region Kong Configuration

    /// <summary>
    /// Writes the Kong API Gateway configuration YAML file.
    /// </summary>
    public static void WriteKongConfig(string path, string anonKey, string serviceKey, string containerPrefix, int goTruePort, int postRestPort, int storagePort, int metaPort, int edgeRuntimePort)
    {
        var yaml = $"""
_format_version: '2.1'
_transform: true

consumers:
  - username: anon
    keyauth_credentials:
      - key: {anonKey}
  - username: service_role
    keyauth_credentials:
      - key: {serviceKey}

acls:
  - consumer: anon
    group: anon
  - consumer: service_role
    group: admin

services:
  - name: auth-v1-open
    url: http://{containerPrefix}-auth:{goTruePort}/verify
    routes:
      - name: auth-v1-open
        strip_path: true
        paths:
          - /auth/v1/verify
    plugins:
      - name: cors

  - name: auth-v1-open-callback
    url: http://{containerPrefix}-auth:{goTruePort}/callback
    routes:
      - name: auth-v1-open-callback
        strip_path: true
        paths:
          - /auth/v1/callback
    plugins:
      - name: cors

  - name: auth-v1-open-authorize
    url: http://{containerPrefix}-auth:{goTruePort}/authorize
    routes:
      - name: auth-v1-open-authorize
        strip_path: true
        paths:
          - /auth/v1/authorize
    plugins:
      - name: cors

  - name: auth-v1
    url: http://{containerPrefix}-auth:{goTruePort}
    routes:
      - name: auth-v1
        strip_path: true
        paths:
          - /auth/v1/
    plugins:
      - name: cors
      - name: key-auth
        config:
          hide_credentials: false
      - name: acl
        config:
          hide_groups_header: true
          allow:
            - admin
            - anon

  - name: rest-v1
    url: http://{containerPrefix}-rest:{postRestPort}
    routes:
      - name: rest-v1
        strip_path: true
        paths:
          - /rest/v1/
    plugins:
      - name: cors
      - name: key-auth
        config:
          hide_credentials: false
      - name: acl
        config:
          hide_groups_header: true
          allow:
            - admin
            - anon

  - name: storage-v1
    url: http://{containerPrefix}-storage:{storagePort}
    routes:
      - name: storage-v1
        strip_path: true
        paths:
          - /storage/v1/
    plugins:
      - name: cors
      - name: key-auth
        config:
          hide_credentials: false
      - name: acl
        config:
          hide_groups_header: true
          allow:
            - admin
            - anon

  - name: meta
    url: http://{containerPrefix}-meta:{metaPort}
    routes:
      - name: meta
        strip_path: true
        paths:
          - /pg/
    plugins:
      - name: key-auth
        config:
          hide_credentials: false
      - name: acl
        config:
          hide_groups_header: true
          allow:
            - admin

  - name: functions-v1
    url: http://{containerPrefix}-edge:{edgeRuntimePort}
    routes:
      - name: functions-v1
        strip_path: false
        paths:
          - /functions/v1/
    plugins:
      - name: cors
      - name: key-auth
        config:
          hide_credentials: false
      - name: acl
        config:
          hide_groups_header: true
          allow:
            - admin
            - anon
""";
        File.WriteAllText(path, yaml);
    }

    #endregion
}

#!/bin/bash

# Truncate Local Database Script for Bares Familia
echo -e "\e[36mIniciando truncado de la base de datos local...\e[0m"

CONTAINER_NAME="bares_local_db"
DB_NAME="baresfamilia_local"
DB_USER="postgres"

# Comprobar si el contenedor está corriendo
DOCKER_RUNNING=$(docker ps --filter "name=${CONTAINER_NAME}" --format "{{.Names}}")

if [ "${DOCKER_RUNNING}" == "${CONTAINER_NAME}" ]; then
    echo -e "\e[33mContenedor '${CONTAINER_NAME}' encontrado. Ejecutando TRUNCATE dinámico...\e[0m"
    
    # Query SQL dinámica para vaciar TODAS las tablas del esquema public (excepto el historial de migraciones)
    SQL=$(cat <<'EOF'
DO $$ 
DECLARE 
    r RECORD; 
BEGIN 
    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory') LOOP 
        EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE'; 
    END LOOP; 
END $$;
EOF
)
    
    echo "${SQL}" | docker exec -i ${CONTAINER_NAME} psql -U ${DB_USER} -d ${DB_NAME}
    
    if [ $? -eq 0 ]; then
        echo -e "\e[32m✅ ¡Todas las tablas de la base de datos local fueron vaciadas con éxito!\e[0m"
        echo -e "\e[90mNota: Si deseas reiniciar el estado de activación del POS, elimina el archivo 'device_activation.json' generado en la carpeta de ejecución de Local.Api.\e[0m"
    else
        echo -e "\e[31m❌ Error al ejecutar el comando en PostgreSQL.\e[0m"
    fi
else
    echo -e "\e[31m⚠️ El contenedor '${CONTAINER_NAME}' no está en ejecución.\e[0m"
    echo -e "\e[33mAsegúrate de iniciar el ecosistema mediante 'docker-compose up --build' antes de ejecutar este script.\e[0m"
fi

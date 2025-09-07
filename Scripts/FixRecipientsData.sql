-- Script para corrigir dados de Recipients em AutomatedAlerts
-- Converte strings simples para formato JSON válido

UPDATE AutomatedAlerts 
SET Recipients = CASE 
    WHEN Recipients IS NULL OR Recipients = '' THEN '[]'
    WHEN Recipients NOT LIKE '[%' AND Recipients NOT LIKE '{%' THEN 
        '["' + REPLACE(Recipients, ',', '","') + '"]'
    ELSE Recipients
END
WHERE Recipients IS NULL 
   OR Recipients = '' 
   OR (Recipients NOT LIKE '[%' AND Recipients NOT LIKE '{%');

-- Verificar os resultados
SELECT Id, Name, Recipients FROM AutomatedAlerts;
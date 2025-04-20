   WITH subset AS (
   SELECT *
   FROM Vocabulary
   WHERE タイプ = '通常'
   ORDER BY 番号
   )
   SELECT *
   FROM subset


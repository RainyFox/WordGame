WITH subset AS (
    SELECT *
      FROM Vocabulary
     WHERE タイプ = '通常'         -- ① 類別篩選
     ORDER BY 番号                 -- ② 先照編號排序
)
SELECT  S.*,                              
        COALESCE(U.Proficiency, 0) AS Proficiency,
		U.Mode
FROM    subset  AS S
LEFT JOIN UserProgress AS U
       ON U.番号 = S.番号                  
ORDER BY 番号

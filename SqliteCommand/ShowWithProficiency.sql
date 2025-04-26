WITH subset AS (
    SELECT *
      FROM Vocabulary
     WHERE タイプ = '通常'         -- ① 類別篩選
     ORDER BY 番号                 -- ② 先照編號排序
     LIMIT 200               -- ③ 取 a..b 範圍
     OFFSET 0
)
SELECT  S.*,                              
        COALESCE(U.Proficiency, 0) AS Proficiency,
		U.LastAnswer
FROM    subset  AS S
LEFT JOIN UserProgress AS U
       ON U.番号 = S.番号                  
ORDER BY LastAnswer

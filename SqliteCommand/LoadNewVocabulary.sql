INSERT INTO Vocabulary
SELECT * FROM New
WHERE New.番号 NOT IN (SELECT 番号 FROM Vocabulary);
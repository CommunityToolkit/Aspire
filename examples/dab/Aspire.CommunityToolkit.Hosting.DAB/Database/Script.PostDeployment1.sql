IF NOT EXISTS(SELECT TOP 1 1 FROM dbo.Users)
		INSERT INTO users
		(Id, FirstName, LastName)
		VALUES 
		(1, 'John', 'Doe'),
		(2, 'Jane', 'Doe');
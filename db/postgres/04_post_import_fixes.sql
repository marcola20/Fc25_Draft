select setval(pg_get_serial_sequence('"Players"', 'PlayerId'), coalesce((select max("PlayerId") from "Players"), 0));
select setval(pg_get_serial_sequence('"Token_Administrador"', 'AdminTokenId'), coalesce((select max("AdminTokenId") from "Token_Administrador"), 0));

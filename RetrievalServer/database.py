import sqlite3
class DBInstance:
    _connection = None
    _cursor = None
    @staticmethod
    def instantiate():
        DBInstance._connection = sqlite3.connect(".\\docs\\loaded.db", check_same_thread=False)
        DBInstance._cursor = DBInstance._connection.cursor()
        DBInstance.create_doc_table("Processed")
        DBInstance.create_doc_table("Loaded")
    
    @staticmethod
    def close():
        DBInstance._connection.close()

    @staticmethod
    def create_doc_table(table_name: str):
        DBInstance._cursor.execute(f"""
        CREATE TABLE IF NOT EXISTS {table_name} (
        id INTEGER PRIMARY KEY,
        docsource TEXT NOT NULL,
        docurl TEXT NOT NULL,
        doctitle TEXT NOT NULL
        )
        """)
        DBInstance._connection.commit()

    @staticmethod
    def reset_table(table: str):
        DBInstance._cursor.execute(f"DROP TABLE IF EXISTS {table}")
        DBInstance.create_doc_table(table)

    @staticmethod
    def add_docdata(table: str, source: str, url: str, title: str):
        DBInstance._cursor.execute(f"INSERT INTO {table}(docsource, docurl, doctitle) VALUES (?, ?, ?)", (source, url, title))
        DBInstance._connection.commit()

    @staticmethod
    def is_in_table(table: str, source: str, url: str, title: str):
        DBInstance._cursor.execute(f'SELECT EXISTS(SELECT 1 FROM {table} WHERE docsource="{source}" LIMIT 1);')
        if DBInstance._cursor.fetchone()[0] == 1:
            return True
        DBInstance.add_docdata(table, source, url, title)
        return False

    @staticmethod
    def get_all_docdata(table: str):
        DBInstance._cursor.execute(f"SELECT * FROM {table}")
        docdata = [(source[1], source[2], source[3]) for source in DBInstance._cursor.fetchall()]
        return docdata
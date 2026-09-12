import subprocess
from datetime import datetime
import tkinter as tk
from tkinter import messagebox, filedialog, Toplevel, ttk, simpledialog
from PIL import Image, ImageTk
import webbrowser
import os
import re
import pyodbc
import sys
import hashlib

HASHED_PASSWORD = "86ec849171b8ffcf7e2505c84fa63b0e"

def verify_pwd(password):
    return hashlib.md5(password.encode()).hexdigest() == HASHED_PASSWORD

def ressource_path(relative_path):
    if hasattr(sys, '_MEIPASS'):
        return os.path.join(sys._MEIPASS, relative_path)
    return os.path.join(os.path.abspath("."), relative_path)

class AuthWindows(tk.Toplevel):
    def __init__(self, parent, callback):
        super().__init__(parent)
        self.callback = callback
        self.title("AIG Toolbox")
        self.geometry("200x110")
        self.iconbitmap(ressource_path("AIG_Toolbox.ico"))
        self.resizable(False,False)

        image_path = ressource_path("LogoAlliancesGroupe.png")
        image = Image.open(image_path)
        image = image.resize((150,19))
        self.img = ImageTk.PhotoImage(image)
        
        self.label_image = tk.Label(self, image=self.img, cursor="hand2")
        self.label_image.pack(pady=5)
        self.label_image.bind("<Button-1>", lambda e: webbrowser.open("https://aig.fr"))

#        self.logo = tk.PhotoImage(file=ressource_path("LogoAlliancesGroupe.png"))
#        tk.Label(self, image=self.logo).pack(pady=5)

        tk.Label(self, text="Mot de passe :").pack(pady=5)
        self.password_entry = tk.Entry(self, show="*")
        self.password_entry.pack(pady=5)
        self.password_entry.bind('<Return>', self.check_password)
#        tk.Button(self, text="Valider", command=self.check_password).pack(pady=5)

        self.update_idletasks()
        width = self.winfo_width()
        height = self.winfo_height()
        x = (self.winfo_screenwidth() // 2) - (width //2)
        y = (self.winfo_screenheight() // 2) - (height // 2)
        self.geometry(f'{width}x{height}+{x}+{y}')

    def check_password(self, event=None):
        if verify_pwd(self.password_entry.get()):
            self.destroy()
            self.callback()
        else:
            messagebox.showerror("Erreur", "Mot de passe incorrect")

class AIGToolBox:

    def insert_logo(self):
        frame_footer = tk.Frame(self.root)
        frame_footer.grid(row=3, column=0, columnspan=2, pady=5)
              
        
        image_path = ressource_path("Logo_AlliancesGroupe2023_RVB.png")
        image = Image.open(image_path)
        image = image.resize((150,19))
        self.img = ImageTk.PhotoImage(image)
        
        self.label_image = tk.Label(frame_footer, image=self.img, cursor="hand2")
        self.label_image.pack(pady=5)
        self.label_image.bind("<Button-1>", lambda e: webbrowser.open("https://aig.fr"))

    def __init__(self, root):
        self.root = root
        self.root.title("AIG Toolbox")
        self.root.iconbitmap(ressource_path("AIG_Toolbox.ico"))
        self.root.resizable(False, False)
        self.connection = None
        self.exercises = {}
              
        # Interface de connexion partie haute / centrer
        frame_top = tk.Frame(root)
        frame_top.grid(row=0, column=0, columnspan=2, pady=5, sticky="n")

        #Champ de saisie pour l'instance SQL
        self.entry_instance = tk.Entry(frame_top, width=40, fg="gray", font=("Arial", 10, "italic"))
        self.set_placeholder(self.entry_instance, "Instance SQL")
        self.entry_instance.pack()

        #Champ de saisie pour la base SQL
        self.entry_database = tk.Entry(frame_top, width=40, fg="gray", font=("Arial", 10, "italic"))
        self.set_placeholder(self.entry_database, "Nom de la base")
        self.entry_database.pack()

        #Libellé d'information
        self.label_info = tk.Label(frame_top, text="Si rien n'est renseigné, Connecter propose de choisir un MAE")
        self.label_info.pack(pady=0)

        #Bouton de connexion a la base de données
        self.btn_connect = tk.Button(frame_top, text="Connecter",command=self.connect_btn_click, width=35)
        self.btn_connect.pack(pady=5)
    
        #Statut de la connexion, affiché en rouge quand non connecté
        self.label_status = tk.Label(frame_top, text="Statut : Non connecté", fg="red")
        self.label_status.pack(pady=5)

        # Partie de l'interface avec 2 colonnes
        frame_main = tk.Frame(root)
        frame_main.grid(row=1, column=0, columnspan=2, pady=10)

        #Colonne 1 (action sur une base précise)
        frame_left = tk.Frame(frame_main)
        frame_left.grid(row=0, column=0, padx=10, sticky="n")

        self.label_text1 = tk.Label(frame_left, text="Actions sur la base de données")
        self.label_text1.pack()
        
        self.btn_db_file = tk.Button(frame_left, text="Créer les fichiers de connexion de la base", command=self.generate_db_files, state=tk.DISABLED, width=35)
        self.btn_db_file.pack(pady=5)

        self.btn_maj_db = tk.Button(frame_left, text="Mettre à jour la base", command=self.maj_db, state=tk.DISABLED, width=35)
        self.btn_maj_db.pack(pady=5)

        self.btn_disconnect = tk.Button(frame_left, text="Déconnexion des utilisateurs fantômes", command=self.cleanup_sessions, state=tk.DISABLED, width=35)
        self.btn_disconnect.pack(pady=5)

        self.btn_accounting = tk.Button(frame_left, text="Déclôture d'exercice", command=self.open_accounting_window, state=tk.DISABLED, width=35)
        self.btn_accounting.pack(pady=5)

        self.btn_decloture_fa = tk.Button(frame_left, text="Déclôture/Dévalidation d'une facture", command=self.open_declo_facture, state=tk.DISABLED, width=35)
        self.btn_decloture_fa.pack(pady=5)

        self.btn_reset_pwd = tk.Button(frame_left, text="Supprimer le mot de passe Administrateur",command=self.reset_admin_password, state=tk.DISABLED, width=35)
        self.btn_reset_pwd.pack(pady=5)

        self.btn_list_triggers = tk.Button(frame_left, text="Lister les triggers spécifiques", command=self.list_triggers, state=tk.DISABLED, width=35)
        self.btn_list_triggers.pack(pady=5)

        self.btn_creation_IL = tk.Button(frame_left, text="Créer une information libre", command=self.open_creation_il, state=tk.DISABLED, width=35)
        self.btn_creation_IL.pack(pady=5)

        self.btn_creation_adresse_livraison = tk.Button(frame_left, text="Créer les adresses de livraison manquantes", command=self.correction_adresse_gescom, state=tk.DISABLED, width=35)
        self.btn_creation_adresse_livraison.pack(pady=5)

        #Colonne 2 (action sur l'instance / ensemble des bases)
        frame_right = tk.Frame(frame_main)
        frame_right.grid(row=0, column=1, padx=10, sticky="n")
        
        self.label_text2 = tk.Label(frame_right, text="Actions sur l'instance SQL")
        self.label_text2.pack()
        
        self.btn_generate_files = tk.Button(frame_right, text="Créer les fichiers pour toutes les bases", command=self.generate_files, width=35)
        self.btn_generate_files.pack(pady=5)
        
        self.btn_maj_all_db = tk.Button(frame_right, text="Mettre à jour les bases", command=self.maj_all_db, width=35)
        self.btn_maj_all_db.pack(pady=5)

        self.btn_users_copy = tk.Button(frame_right, text="Copier des utilisateurs", command=self.open_users_copy, width=35)
        self.btn_users_copy.pack(pady=5)

        #Zone du bas pour logs + logo
        self.text_status = tk.Text(root, height=10, width=70)
        self.text_status.grid(row=2, column=0, columnspan=2, pady=5)

        self.insert_logo()

    #Gestion du texte par defaut pour les champs instance et base sql
    def set_placeholder(self, entry, placeholder_text):
        entry.insert(0, placeholder_text)

        #Si on saisie manuellement la police passe en noir normal
        def on_focus_in(event):
            if entry.get() == placeholder_text:
                entry.delete(0, tk.END)
                entry.config(fg="black", font=("Arial", 10, "normal"))

        #Par defaut c'est en gris italique
        def on_focus_out(event):
            if entry.get() == "":
                entry.insert(0, placeholder_text)
                entry.config(fg="gray", font=("Arial", 10, "italic"))

        entry.bind("<FocusIn>", on_focus_in)
        entry.bind("<FocusOut>", on_focus_out)

    def connect_btn_click(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()

        #On test si une instance ou une base a été saisie, sinon on demande a ouvrir un fichier mae
        if instance == 'Instance SQL' or database == 'Nom de la base':
            self.browse_file()
        else:
            self.connect_to_db(instance, database)  

    #Selection du fichier mae
    def browse_file(self):
        file_path = filedialog.askopenfilename(filetypes=[("Fichiers MAE", "*.mae")])
        if not file_path:
            return
        
        instance, database = self.extract_db_info(file_path)
        if instance and database:
            self.entry_instance.delete(0,tk.END)
            self.entry_database.delete(0,tk.END)

            self.entry_instance.insert(0, instance)
            self.entry_database.insert(0, database)
            self.connect_to_db(instance, database)

    #Extraction des donnée du mae pour determiner l'instance et la base de données
    def extract_db_info(self, file_path):
        with open(file_path, "r", encoding="utf-8") as file:
            lines = file.readlines()

        instance = None
        for line in lines:
            match = re.match(r"ServeurSQL=(.+)", line.strip())
            if match:
                instance = match.group(1)
                break

        database = os.path.splitext(os.path.basename(file_path))[0]

        if instance and database:
            return instance, database
        else:
            messagebox.showerror("Erreur", "Impossible de récupérer les informations de connexions.")
            return None, None

    #Connexion a la base de données
    def connect_to_db(self, instance, database):
        try:
            self.instance = instance
            self.database = database
            self.connection = pyodbc.connect(
                f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;"
            )
            self.label_status.config(text=f"Connecté à {database}", fg="green")
            self.btn_disconnect.config(state=tk.NORMAL)
            self.btn_accounting.config(state=tk.NORMAL)
            self.btn_reset_pwd.config(state=tk.NORMAL)
            self.btn_maj_db.config(state=tk.NORMAL)
            self.btn_db_file.config(state=tk.NORMAL)
            self.btn_list_triggers.config(state=tk.NORMAL)
            self.btn_creation_IL.config(state=tk.NORMAL)
            self.btn_creation_adresse_livraison.config(state=tk.NORMAL)
            self.btn_decloture_fa.config(state=tk.NORMAL)
            self.text_status.insert(tk.END, f"✅ Connexion réussie à {database}.\n")
            self.text_status.see(tk.END)
        except Exception as e:
            messagebox.showerror("Erreur de connexion", str(e))
            self.text_status.insert(tk.END, f"❌ Échec de connexion : {e}\n")
            self.text_status.see(tk.END)

    #Deconnexion des utilisateurs fantomes
    def cleanup_sessions(self):
        if not self.connection:
            return

        #Récupération des utilisateurs fantomes avant suppression
        select_query = """
        SELECT cbUsername FROM cbUserSession where cbSession NOT IN (select spid FROM sys.sysprocesses where dbid = DB_ID()) AND CB_Creator <> 'COBJ';
        """

        try:
            cursor = self.connection.cursor()
            cursor.execute(select_query)
            ghost_users = [row[0] if row[0] is not None else "Inconnu" for row in  cursor.fetchall()]

            #Suppression des utilisateurs fantomes
            delete_query = f"""
            DELETE FROM cbUserSession WHERE cbSession NOT IN (
                SELECT spid FROM sys.sysprocesses WHERE dbid = DB_ID()
            ) AND CB_Creator <> 'COBJ';

            DELETE FROM cbMessage WHERE cbSession NOT IN (SELECT cbSession FROM cbUserSession);
            DELETE FROM cbNotification WHERE cbSession NOT IN (SELECT cbSession FROM cbUserSession);
            DELETE FROM cbRegFile WHERE cbSession NOT IN (SELECT cbSession FROM cbUserSession);
            DELETE FROM cbRegMessage WHERE cbSession NOT IN (SELECT cbSession FROM cbUserSession);
            DELETE FROM cbRegUser WHERE cbSession NOT IN (SELECT cbSession FROM cbUserSession);
            """
            cursor.execute(delete_query)
            self.connection.commit()

            if ghost_users:
                self.text_status.insert(tk.END, f"✅ Utilisateurs fantômes déconnectés : {', '.join(ghost_users)}\n")
                self.text_status.see(tk.END)
            else:
                self.text_status.insert(tk.END, f"⚠️ Aucun utilisateur fantôme à deconnecter.\n")
                self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la suppression des utilisateurs fantômes : {e}\n")
            self.text_status.see(tk.END)

    #Decloture - Choix de l'exercice comptable
    def open_accounting_window(self):
        if not self.connection:
            messagebox.showerror("Erreur", "Vous devez être connecté à une base de données.")
            return

        cursor = self.connection.cursor()
        #Selection des exercices existant dans le dossier comptable
        query = """
            SELECT D_DebutExo01, D_FinExo01, D_DebutExo02, D_FinExo02,
                   D_DebutExo03, D_FinExo03, D_DebutExo04, D_FinExo04,
                   D_DebutExo05, D_FinExo05
            FROM P_DOSSIER
        """
        cursor.execute(query)
        row = cursor.fetchone()
        if not row:
            messagebox.showerror("Erreur", "Impossible de récupérer les exercices comptables.")
            return

        #Mise en forme des exercices pour la fenetre de choix
        self.exercises = {}
        for i in range(5):
            debut, fin = row[i * 2], row[i * 2 + 1]
            if debut and fin:
                # Vérification du type et formatage des dates
                debut_dt = debut if isinstance(debut, datetime) else datetime.strptime(debut, "%Y-%m-%d %H:%M:%S")
                fin_dt = fin if isinstance(fin, datetime) else datetime.strptime(fin, "%Y-%m-%d %H:%M:%S")

                if debut_dt.date() != datetime(1753, 1, 1).date():
                    debut_str = debut_dt.strftime("%d/%m/%Y")
                    fin_str = fin_dt.strftime("%d/%m/%Y")
                    self.exercises[f"Exercice {i+1} ({debut_str} → {fin_str})"] = (debut_dt, fin_dt)

        if not self.exercises:
            messagebox.showinfo("Info", "Aucun exercice valide à afficher.")
            return

        self.accounting_window = Toplevel(self.root)
        self.accounting_window.title("Sélectionnez un exercice")
        self.accounting_window.iconbitmap(ressource_path("AIG_Toolbox.ico"))
        self.accounting_window.resizable(False, False)

        self.selected_exercise = tk.StringVar(value=list(self.exercises.keys())[0])

        for exo in self.exercises.keys():
            tk.Radiobutton(self.accounting_window, text=exo, variable=self.selected_exercise, value=exo).pack(anchor="w")

        tk.Label(self.accounting_window, text="Une sauvegarde est effectué avant traitement").pack(anchor='w', pady=5)
        btn_validate = tk.Button(self.accounting_window, text="Valider", command=self.run_accounting_script)
        btn_validate.pack(pady=10)

    #Decloture comptable
    def run_accounting_script(self):
        selected_exo = self.selected_exercise.get()
        if selected_exo not in self.exercises:
            messagebox.showerror("Erreur", "Sélection invalide.")
            return

        deb_exo, fin_exo = self.exercises[selected_exo]

        confirmation = messagebox.askyesno(
            "Confirmation",
            f"Voulez-vous vraiment déclôturer l'exercice {selected_exo} ?\n\n"
        )

        if not confirmation:
            self.text_status.insert(tk.END, "❌ Traitement annulé par l'utilisateur.\n")
            self.text_status.see(tk.END)
            return
               
        #Sauvegarde de la base de données avant le traitement
        query_backup_path = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(255))"
        cursor = self.connection.cursor()
        cursor.execute(query_backup_path)
        backup_path = cursor.fetchone()[0]
        
        backup_filename = f"{self.database}_avt_traitcpta_{datetime.now().strftime('%Y%m%d')}.bak"
        backup_fullpath = os.path.join(backup_path, backup_filename) 
        backup_command = (
            f"sqlcmd -S {self.instance} -Q \"BACKUP DATABASE [{self.database}] TO DISK='{backup_fullpath}'\""
        )

        try:
            subprocess.run(backup_command, shell=True, check=True)
            self.text_status.insert(tk.END, "✅ Sauvegarde de la base effectuée avec succès.\n")
            self.text_status.see(tk.END)
        except subprocess.CalledProcessError as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la sauvegarde de la base : {e}\n")
            self.text_status.see(tk.END)
            return

        self.accounting_window.destroy()

        try:
            cursor = self.connection.cursor()

            #Récupération de la date de création max
            query_date ="""
            SELECT MAX (cbcreation) FROM F_ECRITUREC where (JM_DATE >= ? AND JM_DATE <= ?)
            """
            cursor.execute(query_date, (deb_exo, fin_exo))
            date_creation = cursor.fetchone()[0]

            if date_creation is None:
                date_creation = "1753-01-01"
            
            #Requete pour decloture des ecritures
            query_update_ecriturec = """
            UPDATE F_ECRITUREC SET EC_Cloture=0, EC_Impression=0, EC_DateCloture='1753-01-01', cbmodification=? WHERE (JM_DATE >= ? AND JM_DATE <= ?)
            """
            cursor.execute(query_update_ecriturec, (date_creation, deb_exo, fin_exo))

            #Requete pour decloture des journaux
            query_update_jmouv = """
            UPDATE F_JMOUV SEt JM_Cloture=0, JM_Impression=0 WHERE (JM_Date >= ? AND JM_Date <= ?)
            """
            cursor.execute(query_update_jmouv, (deb_exo, fin_exo))

            #Requete pour la décloture de la période
            for i in range(1,6):
                query_check_exo = f"SELECT D_DebutExo0{i} FROM P_DOSSIER"
                cursor.execute(query_check_exo)
                exercice_select = cursor.fetchone()[0]

                if exercice_select == deb_exo:
                    query_update_dossier = f"UPDATE P_DOSSIER SET D_Archivage0{i} =0"
                    cursor.execute(query_update_dossier)
            
            #Requete pour la modification de date de cloture / archive
            query_update_archive = """
            UPDATE P_DOSSIER SET D_CloturePeriod = DATEADD(year, -1, ?), D_ArchivePeriod = DATEADD(year, -1, ?)
            """
            cursor.execute(query_update_archive, (fin_exo, fin_exo))

            #Requete pour le nettoyage des lignes de clotures dans le journal des evenement légaux
            query_delete_auditcpta = """
            DELETE FROM F_AUDITCPTA where ADT_Description like '%'+RIGHT('0'+CAST(DAY(?) as  VARCHAR), 2)+'%' and ADT_Description like '%'+RIGHT('0'+CAST(DAY(?) as VARCHAR),2)+'%'
            """
            cursor.execute(query_delete_auditcpta, (deb_exo, fin_exo))

            #Execution des changement
            self.connection.commit()

            self.text_status.insert(tk.END, f"✅ Declôture de l'exercice {selected_exo} effectuée avec succès.\n")
            self.text_status.see(tk.END)

        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors du traitement comptable : {e}\n")
            self.text_status.see(tk.END)
            self.connection.rollback()

    #Suppression du mot de passe lié a l'utilisateur ayant PROT_NO = 1 (<administrateur> par defaut)
    def reset_admin_password(self):
        confirmation = messagebox.askyesno("Confirmation","Voulez-vous vraiment réinitialiser le mot de passe du compte <Administrateur> ?")
        if not confirmation:
            self.text_status.insert(tk.END, "❌ Réinitialisation annulée par l'utilisateur.\n")
            self.text_status.see(tk.END)
            return

        software_check  ="SELECT CB_TYPE FROM cbsystable"
        reset_query = {
            "CPTA": "UPDATE F_PROTECTIONCPTA SET PROT_Guid='d17b5a69-8dd6-42fd-805c-36c5c7419872', PROT_Hash=0x870A3CFBA667D3B6A8B40BA44545F63D2B2012F7A697A43AFD3AA753F618DDE2 WHERE PROT_NO=1",
            "CIAL": "UPDATE F_PROTECTIONCIAL SET PROT_Guid='d17b5a69-8dd6-42fd-805c-36c5c7419872', PROT_Hash=0x870A3CFBA667D3B6A8B40BA44545F63D2B2012F7A697A43AFD3AA753F618DDE2 WHERE PROT_NO=1",
            "TRES": "UPDATE F_PROTECTIONTRES SET PROT_Guid='d17b5a69-8dd6-42fd-805c-36c5c7419872', PROT_Hash=0x870A3CFBA667D3B6A8B40BA44545F63D2B2012F7A697A43AFD3AA753F618DDE2 WHERE PROT_NO=1",
            "IMMO": "UPDATE F_PROTECTIONIMMO SET PROT_Guid='d17b5a69-8dd6-42fd-805c-36c5c7419872', PROT_Hash=0x870A3CFBA667D3B6A8B40BA44545F63D2B2012F7A697A43AFD3AA753F618DDE2 WHERE PROT_NO=1"        
        }
        software_mapping = {
            "CPTA": "Comptabilité",
            "CIAL": "Gestion Commerciale",
            "TRES": "Moyens de Paiement/Trésorerie",
            "IMMO": "Immobilisation"
        }

        try:
            cursor = self.connection.cursor()
            #Récupération des logiciels présents dans la base
            cursor.execute(software_check)
            rows = cursor.fetchall()
            affected_software = []

            for row in rows:
                software_type = row[0]
                if software_type in reset_query:
                    cursor.execute(reset_query[software_type])
                    affected_software.append(software_mapping[software_type])
            
            self.connection.commit()

            if affected_software:
                success_message = "✅ Mot de passe Administrateur réinitialisé pour : " + "/".join(affected_software) + "\n"
                self.text_status.insert(tk.END, success_message)
                self.text_status.see(tk.END)
            else:
                self.text_status.insert(tk.END, "⚠️ Aucun logiciel concerné trouvé pour la réinitialisation.\n")
                self.text_status.see(tk.END)

        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Une erreur est survenue : {e}")
            self.text_status.see(tk.END)
            self.connection.rollback()

    #Generation des fichiers de connexion 
    def generate_db_files(self):
        instance = self.entry_instance.get().strip()
        db_name = self.entry_database.get().strip()

        if instance == 'Instance SQL' or db_name == 'Nom de la base':
            messagebox.showerror("Erreur", "L'instance et le nom de la base doit etre renseigné")
            return
        
        connexion_file_dir = filedialog.askdirectory(title="Choisissez le répertoire de destination des fichiers")
        if not connexion_file_dir:
            return
        
        connexion_file_dir = os.path.normpath(connexion_file_dir)
        self.generate_connexion_files(db_name, connexion_file_dir, instance)

    #Choix du repertoire pour stocké les fichiers de connexion
    def generate_files(self):
        instance = self.entry_instance.get().strip()
        if instance == 'Instance SQL':
            messagebox.showerror("Erreur", "Le nom de l'instance SQL doit être renseigné.")
            return
        
        connexion_file_dir = filedialog.askdirectory(title="Choisissez le répertoire de destination des fichiers")
        if not connexion_file_dir:
            return
        
        connexion_file_dir = os.path.normpath(connexion_file_dir)
        
        db_list = self.get_database_list(instance)
        for db_name in db_list:
            self.generate_connexion_files(db_name, connexion_file_dir, instance)

    def get_database_list(self, instance):
        try:
            connexion = pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};Trusted_connection=yes;")
            cursor = connexion.cursor()
            cursor.execute("SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' and database_id > 4")
            db_list = [row[0] for row in cursor.fetchall()]
            connexion.close()
            return db_list
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la récupération des bases : {e}\n")
            self.text_status.see(tk.END)
            return[]
        
    def generate_connexion_files(self, db_name, connexion_file_dir, instance):
        try:
            connexion_string=f"DRIVER={{SQL Server}};SERVER={instance};Trusted_Connection=yes;DATABASE={db_name}"
            co_gen = pyodbc.connect(connexion_string)

            if self.check_table_exists(instance, db_name, "T_INSTANCES"):
                file_path = os.path.join(connexion_file_dir, f"{db_name}.prh")
                with open(file_path, "w") as f:
                    f.write(f"[CONNECTION SQL]\nProvider=MSOLEDBSQL\nServer={instance}\nTrusted_Connexion=yes\nDatabase={db_name}\nApplication Name=Sage Application\n")
                self.text_status.insert(tk.END, f"✅ Fichier généré : {file_path}\n")
                self.text_status.see(tk.END)

            if self.check_table_exists(instance, db_name, "cbsystable"):
                cursor = co_gen.cursor()
                cursor.execute("SELECT cb_type FROM cbsystable")
                rows = cursor.fetchall()
                
                for row in rows:
                    cb_type = row[0]
                    if cb_type in ["CPTA","CIAL","TRES","IMMO"]:
                        extenstion = {"CPTA":".mae","CIAL":".gcm","TRES":".mdp","IMMO":".imo"}[cb_type]
                        file_path = os.path.join(connexion_file_dir,f"{db_name}{extenstion}")
                        with open(file_path, "w") as f:
                            f.write(f"[CBASE]\nServeurSQL={instance}\n")
                            f.write(f"Createur={self.get_creator(cb_type)}\nType={cb_type}\n")
                        self.text_status.insert(tk.END, f"✅ Fichier généré : {file_path}\n")
                        self.text_status.see(tk.END)

            self.text_status.insert(tk.END, f"✅ Tous les fichiers de connexion ont été générés pour {db_name}.\n")
            self.text_status.see(tk.END)
        
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la génération des fichiers de connexion pour {db_name} : {e}\n")
            self.text_status.see(tk.END)

        finally:
            if 'co_gen' in locals():
                co_gen.close()

    def check_table_exists(self, instance_name, db_name, table_name):
        try:
            connexion = pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance_name};Trusted_Connection=yes;DATABASE={db_name}")
            cursor = connexion.cursor()
            cursor.execute(f"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = ?", (table_name,))
            exists = cursor.fetchone()[0]>0
            connexion.close()
            return exists
        except:
            return False
        
    def get_creator(self, cb_type):
        creators = {
            "CPTA": "MA30",
            "CIAL": "COLU",
            "TRES": "MIMO",
            "IMMO": "MOPA"
        }
        return creators.get(cb_type, "UNKNOWN")

    def get_sql_version(self, co_sql):
        cursor = co_sql.cursor()
        cursor.execute("SELECT LEFT(CAST(SERVERPROPERTY('ProductVersion') AS VARCHAR), 2)")
        result = cursor.fetchone()

        if result:
            try:
                version =int(result[0])
            except ValueError:
                return 110
        else:
            return 110

        compatibility_map = {
            16: 160,
            15: 150,
            14: 140,
            13: 130,
            12: 120,
            11: 110
        }
        return compatibility_map.get(version, 110)

    def maj_all_db(self):
        instance = self.entry_instance.get().strip()
        if instance == 'Instance SQL':
            messagebox.showerror("Erreur", "Vous devez renseigner une instance !")
            return
        
        try:
            co_sql = pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;")
            cursor = co_sql.cursor()

            cursor.execute("SET IMPLICIT_TRANSACTIONS OFF")
            co_sql.commit()

            cursor.execute("SELECT name from sys.databases WHERE name NOT IN ('master','tempdb','model','msdb')")
            databases = [row[0] for row in cursor.fetchall()]

            if not databases:
                self.text_status.insert(tk.END, f"🔎 Aucune base de données n'a été trouvées\n")
                self.text_status.see(tk.END)
                return
            
            compatibility_level = self.get_sql_version(co_sql)

            for db_name in databases:
                self.text_status.insert(tk.END, f"🔄 Mise à jour de la base : {db_name}\n")
                self.text_status.see(tk.END)

                alter_queries = [
                    f"ALTER DATABASE [{db_name}] SET AUTO_CLOSE OFF WITH NO_WAIT",
                    f"ALTER DATABASE [{db_name}] SET RECOVERY SIMPLE WITH NO_WAIT",
                    f"ALTER DATABASE [{db_name}] SET COMPATIBILITY_LEVEL = {compatibility_level}"
                ]
                for query in alter_queries:
                    with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as conn:
                        with conn.cursor() as temp_cursor:
                            temp_cursor.execute(query)
                            conn.commit()
                        
                    self.text_status.insert(tk.END, f"✅ {query}\n")
                    self.text_status.see(tk.END)

                cursor.execute(f"USE [{db_name}]")
                cursor.execute(f"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'cbsystable';")
                table_exists = cursor.fetchone()[0]

                if table_exists:
                    self.text_status.insert(tk.END, f"🔎 {db_name} est une base Sage100, execution de la requete d'autorisation.\n")
                    self.text_status.see(tk.END)
                    authorization_queries = [
                        f"USE MASTER; ALTER AUTHORIZATION ON DATABASE::[{db_name}] TO [sa]",
                        f'USE [{db_name}]; ALTER USER [USER_CBASE] with login=[APPL_CBASE]'
                    ]
                    for query in authorization_queries:
                        cursor.execute(query)
                        co_sql.commit()
                        self.text_status.insert(tk.END, f"✅ {query}\n")
                else:
                    self.text_status.insert(tk.END,f"⚠ La base n'est pas une base Sage 100.\n")
                    self.text_status.see(tk.END)
                
            self.text_status.insert(tk.END, f"✅ Toutes les bases ont été mises à jour avec succès.\n")
            self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur : {str(e)}\n")
            self.text_status.see(tk.END)
        finally:
            if co_sql:
                co_sql.close()

    def maj_db(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()

        try:
            co_sql = pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;")
            cursor = co_sql.cursor()

            cursor.execute("SET IMPLICIT_TRANSACTIONS OFF")
            co_sql.commit()

            compatibility_level = self.get_sql_version(co_sql)
            
            alter_queries = [
                f"ALTER DATABASE [{database}] SET AUTO_CLOSE OFF WITH NO_WAIT",
                f"ALTER DATABASE [{database}] SET RECOVERY SIMPLE WITH NO_WAIT",
                f"ALTER DATABASE [{database}] SET COMPATIBILITY_LEVEL = {compatibility_level}"
            ]
            for query in alter_queries:
                with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;") as conn:
                    with conn.cursor() as temp_cursor:
                        temp_cursor.execute(query)
                        conn.commit()
                        
                self.text_status.insert(tk.END, f"✅ {query}\n")
                self.text_status.see(tk.END)

            cursor.execute(f"USE [{database}]")
            cursor.execute(f"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'cbsystable';")
            table_exists = cursor.fetchone()[0]

            if table_exists:
                self.text_status.insert(tk.END, f"🔎 {database} est une base Sage100, execution de la requete d'autorisation.\n")
                self.text_status.see(tk.END)
                authorization_queries = [
                    f"USE MASTER; ALTER AUTHORIZATION ON DATABASE::[{database}] TO [sa]",
                    f'USE [{database}]; ALTER USER [USER_CBASE] with login=[APPL_CBASE]'
                ]
                for query in authorization_queries:
                    cursor.execute(query)
                    co_sql.commit()
                    self.text_status.insert(tk.END, f"✅ {query}\n")
                    self.text_status.see(tk.END)
            else:
                self.text_status.insert(tk.END,f"⚠ La base n'est pas une base Sage 100.\n")
                self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur : {str(e)}\n")
            self.text_status.see(tk.END)
        finally:
            if co_sql:
                co_sql.close()

    def list_triggers(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()

        try:
            co_sql = pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;")
            cursor = co_sql.cursor()

            query = """
            SELECT name, object_name(parent_id) as table_name 
            FROM sys.triggers 
            WHERE name NOT LIKE 'TG_CBDEL_%' 
            AND name NOT LIKE 'TG_CBINS_%' 
            AND name NOT LIKE 'TG_CBUPD%' 
            AND name NOT LIKE 'TG_INS_%' 
            AND name NOT LIKE 'TG_UPD_%' 
            AND name NOT LIKE 'TG_DEL_%' 
            ORDER BY name;
            """

            cursor.execute(query)
            triggers = cursor.fetchall()

            if not triggers:
                self.text_status.insert(tk.END, f"🔎 Aucun trigger spécifique trouvé dans la base {database}.\n")
                self.text_status.see(tk.END)
            else:
                self.text_status.insert(tk.END, f"📜 Triggers spécifiques présents dans {database} :\n")
                self.text_status.see(tk.END)
                for trigger_name, table_name in triggers:
                    self.text_status.insert(tk.END, f"✅ Trigger: {trigger_name} (Table: {table_name})\n")
                    self.text_status.see(tk.END)
            
            cursor.close()
            co_sql.close()
        
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la récupération des triggers : {str(e)}\n")
            self.text_status.see(tk.END)

    def open_users_copy(self):
        instance = self.entry_instance.get().strip()

        if instance == 'Instance SQL':
            messagebox.showerror("Erreur", "Vous devez renseigner une instance !")
            return

        self.users_copy_window = tk.Toplevel(self.root)
        self.users_copy_window.title("Copie des utilisateurs")
        self.users_copy_window.iconbitmap(ressource_path("AIG_Toolbox.ico"))
        self.users_copy_window.resizable(False, False)

        self.databases = self.get_sage_databases()

        tk.Label(self.users_copy_window, text="Base d'origine :").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.origin_var = tk.StringVar()
        self.origin_combobox = ttk.Combobox(self.users_copy_window, textvariable=self.origin_var, values=self.databases, state = "readonly")
        self.origin_combobox.grid(row=0, column=1, padx=10, pady=5, sticky="w")

        tk.Label(self.users_copy_window, text="Base de destination :").grid(row=1, column=0, padx=10, pady=5, sticky="w")
        self.dest_var = tk.StringVar()
        self.dest_combobox = ttk.Combobox(self.users_copy_window, textvariable=self.dest_var, values=self.databases, state = "readonly")
        self.dest_combobox.grid(row=1, column=1, padx=10, pady=5, sticky="w")

        tk.Label(self.users_copy_window, text="Attention\n Le traitement supprime les utilisateurs de la base de destination\n pour les remplacer par ceux de la base de destination !").grid(row=2, column=0, columnspan=2, padx=10, pady=5, sticky="w")

        tk.Button(self.users_copy_window, text="Exécuter", command=self.execute_users_copy).grid(row=3, column=0, columnspan=2, pady=10)

    def get_sage_databases(self):
        databases = []
        instance = self.entry_instance.get().strip()

        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    cursor.execute("SELECT name FROM sys.databases WHERE database_id > 4")
                    temp_databases = [row[0] for row in cursor.fetchall()]
                    ok_databases = []
                    for db in temp_databases:
                        try:
                            cursor.execute(f"SELECT 1 FROM [{db}].sys.tables WHERE name = 'cbSysTable'")
                            if cursor.fetchone():
                                ok_databases.append(db)
                        except Exception as e:
                            print(f"Erreur accès {db} : {e}")
                            continue
                    return ok_databases
        except Exception as e:
            messagebox.showerror("Erreur", f"Erreur lors de la récupération des bases : {e}")
        return databases

    def check_tables_existance(self, database):
        instance = self.entry_instance.get().strip()
        tables =["F_PROTECTIONCPTA","F_PROTECTIONCIAL","F_PROTECTIONIMMO","F_PROTECTIONTRES"]
        existing_tables = []
        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    for table in tables:
                        cursor.execute(f"SELECT 1 FROM [{database}].sys.tables WHERE name = '{table}'")
                        if cursor.fetchone():
                            existing_tables.append(table)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la vérification des tables : {e}\n")
            self.text_status.see(tk.END)
        return existing_tables

    def check_protno_in_f_collaborateur(self,database):
        instance = self.entry_instance.get().strip()        
        prot_no_list = []
        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    cursor.execute(f"SELECT PROT_NO FROM [{database}].[dbo].[F_COLLABORATEUR] WHERE PROT_NO IS NOT NULL")
                    prot_no_list = [row[0] for row in cursor.fetchall()]
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la vérification des utilisateurs liés aux collaborateurs : {e}\n")
            self.text_status.see(tk.END)
        return prot_no_list

    def execute_users_copy(self):
        instance = self.entry_instance.get().strip()
        base_origine = self.origin_var.get()
        base_destination = self.dest_var.get()

        if base_origine == base_destination:
            messagebox.showerror("Erreur","La base d'origine et de destination doit être différente")
            return
        
        self.users_copy_window.destroy()

        existing_tables = self.check_tables_existance(base_destination)
        if "F_PROTECTIONCPTA" not in existing_tables:
            messagebox.showerror("Erreur", f"La table F_PROTECTIONCPTA n'est pas présente dans la base {base_destination} !")
            return

        prot_no_list = self.check_protno_in_f_collaborateur(base_destination)
        if prot_no_list:
            if messagebox.askyesno("Confirmation", f"Certains utilisateurs de la base {base_destination} sont liés à des collaborateurs.\nVoulez vous les supprimer ?"):
                try:
                    with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as co_sql:
                        with co_sql.cursor() as cursor:
                            cursor.execute(f"UPDATE [{base_destination}].[dbo].[F_COLLABORATEUR] SET PROT_NO = NULL WHERE PROT_NO IS NOT NULL")
                            co_sql.commit()
                except Exception as e:
                    self.text_status.insert(tk.END, f"❌ Erreur lors de la suppression des utilisateurs dans les collaborateurs : {e}\n")
                    self.text_status.see(tk.END)
                    return

        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE=master;Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    for table in existing_tables:
                        e_table = f"F_E{table[2:]}"
                        cursor.execute(f"DELETE FROM [{base_destination}].[dbo].[{e_table}]")
                        cursor.execute(f"DELETE FROM [{base_destination}].[dbo].[{table}] WHERE PROT_NO <> 1")
                        cursor.execute(f"INSERT INTO [{base_destination}].[dbo].[{table}] ([PROT_USER],[PROT_DESCRIPTION],[PROT_RIGHT],[PROT_NO],[PROT_EMAIL],[PROT_USERPROFIL],[PROT_ADMINISTRATOR],[PROT_PWDSTATUS],[PROT_DISABLED],[PROT_APPLICATIONRIGHT],[PROT_ALLOWEXTERNALACCESS],[PROT_GUID],[PROT_HASH],[PROT_PROVIDERTYPE],[PROT_PROVIDERID_AAD],[PROT_PROVIDERID_AD],[PROT_SERVICEOFFICE365ACTIF],[PROT_SERVICEOFFICE365USERID],[PROT_SERVICESAGEIDACTIF],[PROT_SERVICESAGEIDUSERID]) SELECT [PROT_USER],[PROT_DESCRIPTION],[PROT_RIGHT],[PROT_NO],[PROT_EMAIL],[PROT_USERPROFIL],[PROT_ADMINISTRATOR],[PROT_PWDSTATUS],[PROT_DISABLED],[PROT_APPLICATIONRIGHT],[PROT_ALLOWEXTERNALACCESS],[PROT_GUID],[PROT_HASH],[PROT_PROVIDERTYPE],[PROT_PROVIDERID_AAD],[PROT_PROVIDERID_AD],[PROT_SERVICEOFFICE365ACTIF],[PROT_SERVICEOFFICE365USERID],[PROT_SERVICESAGEIDACTIF],[PROT_SERVICESAGEIDUSERID] FROM [{base_origine}].[dbo].[{table}] WHERE PROT_NO <> 1")
                        cursor.execute(f"INSERT INTO [{base_destination}].[dbo].[{e_table}] ([PROT_NO],[EPROT_Cmd],[EPROT_RIGHT]) SELECT [PROT_NO],[EPROT_Cmd],[EPROT_RIGHT] FROM [{base_origine}].[dbo].[{e_table}]")
                    co_sql.commit()
            self.text_status.insert(tk.END, f"✅ Copie des utilisateurs effectées avec succès de {base_origine} vers {base_destination}\n")
            self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la copie des utilisateurs : {e}\n")
            self.text_status.see(tk.END)

    def open_creation_il(self):
        self.creation_il_window = tk.Toplevel(self.root)
        self.creation_il_window.title("Creation d'information libre")
        self.creation_il_window.iconbitmap(ressource_path("AIG_Toolbox.ico"))
        self.creation_il_window.resizable(False, False)        

        self.tables = ["F_COMPTEG", "F_COMPTEA", "F_COMPTET", "F_ECRITUREC", "F_IMMOBILISATION", "F_RESSOURCEPROD", "F_DOCENTETE", "F_DOCLIGNE", "F_ABOENTETE", "F_ABOLIGNE"]
        self.types_info = {
            "Texte": (9,70, "VARCHAR"),
            "Date": (3,2, "smalldatetime"),
            "Date Longue": (14, 4, "datetime"),
            "Montant": (20, 8, "numeric(24,6)"),
            "Table" : (22, 36, "VARCHAR(35)"),
            "Valeur": (7, 8, "numeric(24,6)")
        }

        tk.Label(self.creation_il_window, text="Table :").grid(row=0, column=0, padx=10, pady=5, sticky="w")
        self.table_var = tk.StringVar()
        self.table_combobox = ttk.Combobox(self.creation_il_window, textvariable=self.table_var, values=self.tables, state="readonly")
        self.table_combobox.grid(row=0, column=1, padx=10, pady=5, sticky="w")

        tk.Label(self.creation_il_window, text="Nom :").grid(row=1, column=0, padx=10, pady=5, sticky="w")
        self.name_entry = tk.Entry(self.creation_il_window)
        self.name_entry.grid(row=1, column=1, padx=10, pady=5, sticky="w")

        tk.Label(self.creation_il_window, text="Type :").grid(row=2, column=0, padx=10, pady=5, sticky="w")
        self.type_var = tk.StringVar()
        self.type_combobox = ttk.Combobox(self.creation_il_window, textvariable=self.type_var, values=list(self.types_info.keys()), state="readonly")
        self.type_combobox.grid(row=2, column=1, padx=10, pady=5, sticky="w")
        self.type_combobox.bind("<<ComboboxSelected>>", self.toggle_length_entry)

        tk.Label(self.creation_il_window, text="Longueur :").grid(row=3, column=0, padx=10, pady=5, sticky="w")
        self.length_entry = tk.Entry(self.creation_il_window, state="disabled")
        self.length_entry.grid(row=3, column=1, padx=10, pady=5, sticky="w")
        
        tk.Button(self.creation_il_window, text="Créer", command=self.create_info_libre).grid(row=4, column=0, columnspan=2, pady=10)

    def toggle_length_entry(self, event):
        selected_type = self.type_var.get()
        if selected_type == "Texte":
            self.length_entry.config(state="normal")
        else:
            self.length_entry.config(state="disabled")

    def create_info_libre(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()
        table = self.table_var.get()
        name = self.name_entry.get()
        type_info = self.type_var.get()
        length = self.length_entry.get() if self.type_var.get() == "Texte" else None

        if not table or not name or not type_info:
            messagebox.showerror("Erreur","Tous les champs doivent être remplis.")
            return

        cb_type, cb_len, sql_type, = self.types_info[type_info]
        if type_info == "Texte" and length:
            try:
                cb_len = int(length)+1
                sql_type = f"VARCHAR({int(length)})"
            except ValueError:
                messagebox.showerror("Erreur","La longueur doit être un nombre valide")
                return
        
        alter_query = f"ALTER TABLE {table} ADD [{name}] {sql_type} NULL;"
        insert_query = f"INSERT INTO cbSysLibre (CB_File, CB_Name, CB_Pos, CB_Type, CB_Len, CB_Flag) VALUES ('{table}', '{name}', (SELECT MAX(CB_Pos)+1 FROM cbSysLibre WHERE CB_File = '{table}'), {cb_type}, {cb_len}, 0)"

        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    cursor.execute(alter_query)
                    cursor.execute(insert_query)
                    co_sql.commit()
            self.text_status.insert(tk.END, f"✅ L'information libre {name} de type {type_info} a été créée avec succès sur la table {table}\n")
            self.text_status.see(tk.END)
            self.creation_il_window.destroy()
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la création de l'information libre {name} sur la table {table} : {e}\n")
            self.text_status.see(tk.END)

    def correction_adresse_gescom(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()

        query_insert = """
        DECLARE @lino INT;
        SELECT @lino = ISNULL(MAX(LI_No), 0) FROM F_LIVRAISON;
        
        INSERT INTO F_LIVRAISON (LI_No, CT_Num, LI_Intitule, LI_Adresse, LI_Complement, LI_CodePostal, LI_Ville, LI_CodeRegion, LI_Pays, LI_Contact, N_Expedition, N_Condition, LI_Telephone, LI_Telecopie, LI_EMail, LI_DelaiTransport, LI_Commentaire, LI_Principal, LI_AdresseFact)
        SELECT @lino + ROW_NUMBER() OVER(ORDER BY CT_Num ASC), CT_Num, CT_Intitule, CT_Adresse, CT_Complement, CT_CodePostal, CT_Ville, CT_CodeRegion, CT_Pays, CT_Contact, N_Expedition, N_Condition, CT_Telephone, CT_Telecopie, CT_EMail, CT_DelaiTransport, CT_Commentaire, 1, 0
        FROM F_COMPTET
        WHERE NOT EXISTS (
            SELECT 1 FROM F_LIVRAISON AS FL WHERE FL.CT_Num = F_COMPTET.CT_Num AND FL.LI_Principal = 1
        ) AND CT_Type = 0;
        """

        query_update = """
         UPDATE F_DOCENTETE
        SET LI_No = F_LIVRAISON.LI_No
        FROM F_DOCENTETE
        INNER JOIN F_LIVRAISON ON F_DOCENTETE.DO_Tiers = F_LIVRAISON.CT_Num
        WHERE F_DOCENTETE.LI_No = 0;
        """

        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    cursor.execute(query_insert)
                    cursor.execute(query_update)
                    co_sql.commit()
            self.text_status.insert(tk.END, f"✅ Adresses de livraison mises à jour avec succès.\n")
            self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"❌ Erreur lors de la mise à jour des adresses de livraisons : {e}\n")
            self.text_status.see(tk.END)

    def open_declo_facture(self):
        instance = self.entry_instance.get().strip()
        database = self.entry_database.get().strip()

        num_facture = simpledialog.askstring("N° de facture","Entrez le n° de facture :")
        if not num_facture:
            return
        try:
            with pyodbc.connect(f"DRIVER={{SQL Server}};SERVER={instance};DATABASE={database};Trusted_Connection=yes;") as co_sql:
                with co_sql.cursor() as cursor:
                    cursor.execute("UPDATE F_DOCENTETE SET DO_Valide = 0 WHERE DO_Piece = ?", (num_facture,))
                    cursor.execute("UPDATE F_DOCENTETE SET DO_Imprim = 0 WHERE DO_Piece = ?", (num_facture,))
                    cursor.execute("UPDATE F_DOCENTETE SET DO_Type = 6 WHERE DO_Piece = ?", (num_facture,))
                    cursor.execute("UPDATE F_DOCLIGNE SET DO_Type = 6 WHERE DO_Piece = ?", (num_facture,))
                    co_sql.commit()
            self.text_status.insert(tk.END, f"✅ Mise à jour de la facture {num_facture} effectuée avec succès.\n")
            self.text_status.see(tk.END)
        except Exception as e:
            self.text_status.insert(tk.END, f"Erreur lors de la mise à jour de la facture {num_facture} : {e}\n")
            self.text_status.see(tk.END)

#root = tk.Tk()
#app = AIGToolBox(root)
#root.mainloop()
def main():
    root = tk.Tk()
    root.withdraw()

    def launch_app():
        root.deiconify()
        app = AIGToolBox(root)
        root.mainloop()

    auth_window = AuthWindows(root, launch_app)
    root.mainloop()

if __name__ == "__main__":
    main()

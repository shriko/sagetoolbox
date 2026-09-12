# Synthèse — AIG Toolbox V2

Application desktop Windows interne pour l'administration de bases **Sage 100** (comptabilité, gestion commerciale, trésorerie, immobilisations) hébergées sur **SQL Server**. Connexion en **authentification Windows intégrée** via **ODBC** (pyodbc), lecture/écriture directe dans les tables Sage (pas d'API Sage officielle utilisée).

Stack actuelle : Python 3, **CustomTkinter** (UI), **pyodbc** (SQL Server), **Pillow** (images), packagée en exécutable via PyInstaller (.spec présents).

La V1 (`AIG_Toolbox.py`, ~1074 lignes, Tkinter pur) a été comparée à la V2 pour s'assurer de ne rater aucune fonctionnalité historique : **la V2 est un sur-ensemble strict de la V1**, avec 3 ajouts (contrôle d'intégrité comptable, contrôle d'intégrité gestion commerciale, correction des accents) et un réarrangement de l'UI (cards au lieu de boutons empilés). Rien n'a été perdu dans le portage V1→V2.

---

## 1. Fonctionnalités — liste exhaustive

### Authentification / démarrage
1. **Mot de passe d'accès applicatif** au lancement (avant même la fenêtre principale) — hash PBKDF2-HMAC-SHA256 (200 000 itérations, sel fixe) codé en dur dans le binaire, comparaison en temps constant (`hmac.compare_digest`). Fermeture de l'appli si annulé.

### Connexion à une base Sage
2. **Connexion manuelle** : saisie "Instance SQL" + "Base de données" → test de connexion ODBC (Windows Auth), sélection auto du meilleur driver ODBC disponible (18 → 17 → 13 → Native Client 11 → "SQL Server"), `TrustServerCertificate=yes` si driver 18.
3. **Connexion via fichier `.mae`** (fichier de paramétrage Sage) : parsing du fichier pour extraire `ServeurSQL=`, nom de base déduit du nom de fichier, connexion automatique enchaînée.
4. **Mémorisation de la dernière connexion** (instance + base) dans `%APPDATA%\AIG_Toolbox\config.json`, pré-remplissage au démarrage suivant.
5. **Panneau d'information sur la base connectée** (sidebar) : produits Sage installés (Comptabilité/Gestion Commerciale/Trésorerie/Immobilisations, déduits de `cbsystable`), taille des fichiers (Data/Log/FileStream en Mo), chemin physique des fichiers, chargé en asynchrone après connexion.

### Maintenance de base (niveau base sélectionnée)
6. **Optimiser la base** : `AUTO_CLOSE OFF`, `RECOVERY SIMPLE`, ajustement du `COMPATIBILITY_LEVEL` selon la version SQL Server détectée (mapping 11→18 vers 110→180), `ALTER AUTHORIZATION` vers `sa`, réassociation du user Sage `USER_CBASE` au login `APPL_CBASE`.
7. **Générer les fichiers de connexion** de la base courante (voir détail technique plus bas) dans un dossier choisi par l'utilisateur.
8. **Lister les triggers spécifiques** : triggers SQL Server présents sur la base qui ne suivent pas les conventions de nommage standard Sage (`TG_CBxxx_*`, `TG_INS/UPD/DEL_*`) — détecte les customisations/triggers tiers.
9. **Contrôle d'intégrité comptable** (lecture seule, 6 contrôles) :
   - comptes généraux référencés dans les écritures mais absents de `F_COMPTEG`
   - comptes tiers référencés mais absents de `F_COMPTET`
   - codes journaux référencés mais absents de `F_JOURNAUX`
   - écritures déséquilibrées (débit ≠ crédit) regroupées par (pièce, journal, date)
   - écritures analytiques orphelines (`F_ECRITUREA` sans écriture comptable correspondante)
   - écritures analytiques pointant vers un compte analytique inexistant
10. **Contrôle d'intégrité gestion commerciale** (lecture seule, 5 contrôles) :
    - tiers inexistant sur entêtes de documents vente/achat
    - dépôt de stockage inexistant
    - collaborateur inexistant
    - tiers payeur/encaisseur inexistant
    - lignes de documents orphelines (sans entête correspondante, clé = domaine+type+pièce)

### Opérations métier (niveau base sélectionnée)
11. **Déclôture d'exercice comptable** : liste les exercices définis dans `P_DOSSIER` (support V12+ à 10 exercices ou legacy à 5), affichage avec statut Ouvert/Clôturé, sélection par bouton radio, case à cocher **sauvegarde automatique** de la base avant l'opération (`BACKUP DATABASE`, **cochée par défaut**, et si elle échoue la déclôture est annulée), **règle métier bloquante** : impossible de rouvrir un exercice si un exercice plus récent est encore clôturé (dépendance des à-nouveaux) → dans ce cas message d'erreur explicite (redondante côté UI *et* côté repository, donc vérifiée deux fois). Réinitialise `EC_Cloture`/`EC_Impression` sur `F_ECRITUREC`, `JM_Cloture`/`JM_Impression` sur `F_JMOUV`, le flag `D_ArchivageNN` de l'exercice sur `P_DOSSIER`, recale `D_CloturePeriod`/`D_ArchivePeriod` sur la fin de l'exercice précédent, et purge spécifiquement les lignes d'audit `F_AUDITCPTA` dont la description contient "Clock" (pas une purge générale de l'audit).
12. **Dévalider une facture** : saisie d'un n° de pièce (`DO_Piece`) → repasse l'entête et les lignes en type "brouillard" (`DO_Valide=0`, `DO_Imprim=0`, `DO_Type=6`).
13. **Créer une Information Libre** : sur une table cible parmi `F_COMPTEG, F_COMPTEA, F_COMPTET, F_ECRITUREC, F_IMMOBILISATION, F_DOCENTETE, F_DOCLIGNE` ; choix du nom (validé par regex identifiant SQL), du type (Texte/Date/Montant/Valeur/Table dans l'UI actuelle, chacun mappé à un type SQL et un type interne Sage `cbSysLibre`), de la longueur pour le texte (1-255) ; ajout de colonne (`ALTER TABLE`) + enregistrement dans le catalogue Sage `cbSysLibre` avec position auto-incrémentée ; **miroir automatique** vers `F_ABOENTETE`/`F_ABOLIGNE` si la table cible est `F_DOCENTETE`/`F_DOCLIGNE` (tables d'abonnement). *(Note portage : la couche métier `StructureRepository` gère en réalité un 6ᵉ type, "Date Longue" (`datetime`), non exposé dans le menu déroulant de la fenêtre actuelle — à inclure ou non dans l'UI de la réécriture selon décision produit.)*
14. **Corriger les adresses de livraison manquantes** : crée une adresse de livraison principale à partir de la fiche tiers pour tout tiers client sans adresse de livraison, puis rattache les documents orphelins (`LI_No=0`) à la bonne adresse.
15. **Corriger les accents (encodage UTF-8 mal interprété)** : remplace les séquences typiques d'un mauvais décodage (`Ã©`→`é`, `Ã¨`→`è`, etc.) dans les intitulés de `F_ECRITUREC`, `F_COMPTEG`, `F_COMPTET`, `F_COMPTEA`, avec confirmation utilisateur listant les tables impactées.

### Administration / sécurité (niveau base sélectionnée)
16. **Supprimer le mot de passe administrateur** : réinitialise le GUID/hash de protection (`PROT_Guid`/`PROT_Hash`, valeurs par défaut connues) pour l'utilisateur `PROT_NO=1` sur chaque module Sage installé (CPTA/CIAL/TRES/IMMO), détection auto des modules présents via `cbsystable`.
17. **Déconnexion des utilisateurs fantômes** : identifie les sessions Sage (`cbUserSession`) dont le SPID SQL Server n'existe plus (session zombie), purge en cascade les tables liées (`cbMessage`, `cbNotification`, `cbRegFile`, `cbRegMessage`, `cbRegUser`), exclut les sessions système `CB_Creator = 'COBJ'`.

### Opérations niveau instance/serveur (toutes bases Sage)
18. **Optimiser TOUTES les bases** : découvre les bases Sage sur l'instance (bases online avec table `cbsystable`) puis applique l'optimisation (#6) à chacune, sur **une connexion réutilisée** (optimisation perf pour éviter les latences répétées de connexion).
19. **Générer TOUS les fichiers de connexion** : applique #7/#14 à chaque base Sage détectée sur l'instance, dans un dossier choisi.
20. **Copier les utilisateurs entre deux bases** : sélection base source / base destination (listes déroulantes des bases Sage de l'instance), copie complète des tables de protection (`F_PROTECTIONCPTA/CIAL/IMMO/TRES` et leurs tables de droits étendus `F_Exxx`) source→destination avec **écrasement** des utilisateurs existants côté destination (hors compte admin `PROT_NO=1`), vérification préalable que la destination a bien une structure de protection valide (présence de `F_PROTECTIONCPTA`), confirmation explicite car destructif. Avant la copie, **délie tous les collaborateurs** de la base destination de leur profil de protection (`F_COLLABORATEUR.PROT_NO = NULL`), puisque les `PROT_NO` vont être recréés avec de nouvelles valeurs issues de la source.

### Génération de fichiers de connexion (détail technique, utilisé par #7/#14/#19)
- **`.prh`** (connexion OLE DB générique) si la base a une table `T_INSTANCES` : `Provider=MSOLEDBSQL`, serveur, base, `Trusted_Connexion=yes`.
- **`.mae` / `.gcm` / `.mdp` / `.imo`** (fichiers propriétaires Sage par module — Compta/Gestion Co./Trésorerie/Immobilisations) si `cbsystable` présente, un fichier par produit Sage installé, avec le "créateur" applicatif correspondant (`MA30`, `COLU`, `MIMO`, `MOPA`).

### Journalisation / diagnostics transverses
21. **Console de log intégrée** dans la fenêtre principale : horodatage, icônes par niveau (info ℹ️/succès ✅/erreur ❌/avertissement ⚠️), alimentée par une file thread-safe (`queue.Queue`), rafraîchie par polling (100 ms).
22. **Persistance des logs sur disque** : chaque entrée est aussi écrite dans `%APPDATA%\AIG_Toolbox\aig_toolbox.log`.
23. **Confirmation systématique avant toute action** (sauf les actions qui ouvrent leur propre fenêtre de paramétrage) : boîte de dialogue Oui/Non "Lancer *action* ?".
24. **Exécution asynchrone** : toute action longue tourne sur un thread séparé, boutons désactivés + curseur "sablier" pendant le traitement, réactivation à la fin. Deux familles d'actions cohabitent : (a) les actions "directes" (ex. Optimiser la base, Purge fantômes) passent par un flux générique confirmation Oui/Non → thread de fond → réactivation ; (b) les actions qui ouvrent leur propre fenêtre modale de paramétrage (Déclôture, Copie utilisateurs, Info Libre, Dévalidation facture, génération de fichiers, correction des accents) s'exécutent volontairement sur le thread principal Tkinter jusqu'à l'ouverture de la fenêtre/du `filedialog` (Tkinter n'est pas thread-safe), puis lancent elles-mêmes leur propre thread de fond une fois les paramètres saisis et confirmés.
25. **Bascule de thème** Système / Clair / Sombre (CustomTkinter appearance mode).
26. **DPI awareness** Windows (rendu net sur écrans haute résolution).

---

## 2. Description de l'interface

### Fenêtre 1 — Authentification (modale, avant tout)
- Fenêtre fixe 300×250, non redimensionnable, toujours au premier plan (`topmost`).
- Logo Alliances Groupe en haut.
- Libellé "Mot de passe requis" + champ mot de passe masqué (`show="*"`, validation par Entrée ou bouton "Valider").
- Échec → `messagebox` d'erreur, réessai possible. Fermeture de la fenêtre = fermeture de toute l'application.

### Fenêtre principale — Layout "Sidebar + Contenu" (1300×850, centrée à l'écran)
**Sidebar gauche (200px, non redimensionnable)** :
- Logo Alliances Groupe (haut)
- Libellé "Administration Sage"
- Bloc "Info. Base en cours" : nom base, liste des produits Sage installés, tailles Data/Log/FS, chemin (tronqué), mis à jour après connexion
- Sélecteur de thème (Système/Light/Dark) en bas

**Zone principale (droite)**, de haut en bas :
1. **Barre de connexion** (frame horizontale) : champ Instance SQL, champ Base de données, bouton "Connecter", bouton secondaire "Ouvrir fichier local (.mae)".
2. **Dashboard scrollable**, organisé en **cartes (cards)** à coins arrondis, disposées en grille 3 colonnes :
   - 🔧 **Maintenance (Base)** : Optimiser la base, Générer fichiers de connexion, Lister triggers, Contrôle d'intégrité comptable, Contrôle d'intégrité gestion commerciale
   - 💳 **Opérations (Base)** : Déclôture d'exercice, Dévalider une facture, Créer Information Libre, Corriger adresses livraison, Corriger les accents
   - 🛡️ **Administration (Base)** : Supprimer mot de passe, Déconnexion users fantômes
   - ⚙️ **Serveur & Instance** (carte pleine largeur, 3 boutons) : Optimiser TOUTES les bases, Générer TOUS les fichiers, Copier les utilisateurs
   - Tous les boutons "niveau base" et "niveau instance" sont **désactivés tant qu'aucune connexion n'est établie**, puis réactivés après connexion réussie.
3. **Console de log** en bas (zone de texte non éditable, hauteur fixe 180px, police monospace), fond distinct clair/sombre selon thème.

### Fenêtres secondaires (Toplevel modales, `grab_set` + toujours au premier plan)
- **Déclôture d'exercice** (450×550) : liste de radio-boutons (un par exercice, libellé avec dates + statut), case à cocher "Sauvegarder avant", bouton Valider.
- **Copie d'utilisateurs** (400×350) : deux combobox (Source/Destination, listes des bases Sage), avertissement rouge, bouton Copier.
- **Créer Information Libre** (400×400) : combobox Table, champ texte Nom, combobox Type (avec activation/désactivation dynamique du champ Longueur selon le type), bouton Créer.
- **Dévalider Facture** (300×150) : simple champ texte + bouton.
- Chaque fenêtre gère sa propre validation, confirmation et lancement du traitement en thread, puis se ferme immédiatement (le suivi se fait via la console de log de la fenêtre principale).

### Comportements transverses de l'UI
- Toutes les fenêtres/dialogues s'ouvrent centrées, forcées au premier plan (`lift`/`focus_force`) pour éviter qu'elles apparaissent masquées.
- Icône d'application (.ico) et logo (.png) réutilisés partout via un `resource_path` compatible PyInstaller.
- Pas de barre de menu ; toute la navigation se fait par les cartes de boutons.

---

## 3. Plan / architecture du programme (pour portage vers une autre techno)

Architecture en couches, séparation claire UI / logique métier / accès données — un bon point de départ pour une réécriture (ex. Electron+SQL driver natif, WPF/.NET, Tauri+Rust, ou web app + backend) :

```
Point d'entrée
 └─ démarre l'UI, gère DPI awareness (si Windows)

Couche UI
 ├─ Fenêtre d'authentification (gate applicatif, indépendant de la BDD)
 ├─ Fenêtre principale
 │   ├─ Sidebar (logo, infos base connectée, thème)
 │   ├─ Barre de connexion (saisie manuelle + import .mae)
 │   ├─ Dashboard (cartes de fonctionnalités groupées par thème)
 │   └─ Console de log (vue + fichier)
 └─ Fenêtres modales par fonctionnalité paramétrable
     (Déclôture, Copie utilisateurs, Info Libre, Dévalidation facture)

Couche "Core" (transverse)
 ├─ ConfigManager      → persistance JSON (dernière connexion) dans le dossier data utilisateur
 ├─ Logger             → file de log thread-safe + fichier + niveaux (info/success/warning/error)
 ├─ DatabaseManager    → singleton, connexion ODBC/SQL, connexion persistante par thread,
 │                        connexion "autocommit" à la demande pour DDL/BACKUP,
 │                        exécution de requêtes simples et de transactions multi-requêtes,
 │                        détection auto du meilleur driver ODBC disponible,
 │                        substitution de placeholder de schéma ({schema} → [dbo])
 └─ Utils              → résolution de chemin de ressources, centrage de fenêtre

Couche "Repositories" (un par domaine métier, tous dépendants de DatabaseManager)
 ├─ ServerRepository            → découverte des bases Sage, infos base, optimisation,
 │                                 backup, correction d'encodage
 ├─ AdminRepository             → reset mot de passe admin, purge sessions fantômes
 ├─ ConnectionFileRepository    → génération des fichiers de connexion (.prh/.mae/.gcm/.mdp/.imo)
 ├─ AccountingRepository        → lecture des exercices comptables, logique de déclôture
 ├─ TiersRepository             → réparation des adresses de livraison
 ├─ UsersRepository             → copie des droits/utilisateurs entre bases
 ├─ StructureRepository         → création d'informations libres, triggers non standards
 ├─ DocumentRepository          → dévalidation de facture
 ├─ IntegrityRepository         → contrôles d'intégrité comptable (lecture seule)
 └─ CommercialIntegrityRepository → contrôles d'intégrité gestion commerciale (lecture seule)
```

### Points d'architecture à préserver dans la réécriture
1. **Connexion en authentification Windows intégrée à SQL Server** (pas de user/password Sage) — vérifier l'équivalent driver/lib dans la nouvelle techno (ex. `mssql` Node avec `trustedConnection`, ADO.NET `Integrated Security=true`).
2. **Requêtes paramétrées partout** (déjà le cas, sauf noms de table/colonne interpolés — mais ceux-ci sont validés par regex ou proviennent de listes fermées, pas de saisie libre directe dans le SQL).
3. **Transactions atomiques** pour toute séquence de plusieurs UPDATE/DELETE liés (déclôture, purge fantômes, copie utilisateurs) — rollback global en cas d'échec partiel.
4. **Toute opération d'écriture doit rester asynchrone/non bloquante** pour l'UI, avec verrouillage des actions pendant l'exécution et restitution d'un compte-rendu dans le log.
5. **Règles métier à ne pas perdre** :
   - déclôture : blocage si un exercice plus récent est encore clôturé,
   - fenêtre I.Libre : validation stricte du nom (identifiant SQL),
   - copie utilisateurs : vérification de structure de la base destination avant écrasement,
   - contrôles d'intégrité : strictement lecture seule, plafond de lignes remontées (anti-flood UI).
6. **Génération de fichiers de connexion multi-format** selon les modules Sage réellement installés sur chaque base (détection dynamique via `cbsystable`), pas une liste statique.
7. **Journalisation double** (UI temps réel + fichier persistant) à conserver telle quelle pour le support/diagnostic.
8. **Config utilisateur persistée hors du répertoire d'installation** (dossier data utilisateur du profil), avec migration depuis un éventuel ancien emplacement.

### Ce qui est spécifique à Sage/SQL Server et non à l'UI (donc à documenter séparément si besoin)
Les noms de tables (`F_COMPTEG`, `F_COMPTET`, `F_DOCENTETE`, `cbSysLibre`, `cbUserSession`, `P_DOSSIER`, etc.) et leurs relations constituent le "schéma métier Sage" — ils resteront identiques quelle que soit la techno de réécriture, puisqu'ils décrivent la base cible et non l'application elle-même.

---

**Fichiers non repris dans les fonctionnalités** (utilitaires hors app) : `check_drivers.py` (script de diagnostic autonome listant les drivers ODBC installés), `core/perf_debug.py` (traces de debug temporaires pour un problème de tremblement UI, à ne pas reproduire dans la réécriture — c'était un correctif spécifique à CustomTkinter).

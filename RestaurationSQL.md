# Synthèse fonctionnelle — RestaurationSQL

> Reconstituée par décompilation de `RestaurationSQL.exe` (.NET Framework 4.5, sources perdues).

## Objectif

Automatiser la restauration en lot de bases SQL Server à partir d'un dossier de fichiers `.bak`, en redirigeant les fichiers physiques (`.mdf`/`.ldf`) vers un nouvel emplacement, avec option d'écrasement des bases existantes.

## Entrées nécessaires

| Paramètre | Description |
|---|---|
| `cheminBak` | Dossier contenant les `.bak` à traiter (traitement de **tous** les `.bak` du dossier) |
| `nomInstance` | Instance SQL cible (ex: `SRV01\SQLEXPRESS`), connexion en **auth Windows intégrée** |
| `cheminLdfMdf` | Dossier de destination pour les fichiers physiques de données/log |
| `avecRemplacement` | bool — écrase une base existante du même nom si `true` |

## Algorithme (par fichier `.bak` du dossier)

1. **Connexion** : `Data Source={instance};Initial Catalog=master;Integrated Security=True`

2. **Identifier le nom de la base** contenue dans le fichier :
   ```sql
   RESTORE HEADERONLY FROM DISK = '{cheminBak}\{fichier.bak}'
   -- → colonne DatabaseName
   ```
   Le nom récupéré est nettoyé (`Replace(" ", "_")`) pour servir de nom de base final.

3. **Vérifier l'existence** de la base cible en tentant une connexion dessus.
   - Si elle existe et `avecRemplacement = false` → **skip**, log "existe déjà".
   - Si elle existe et `avecRemplacement = true` → la passer offline pour la libérer :
     ```sql
     ALTER DATABASE [NomBase] SET OFFLINE WITH ROLLBACK IMMEDIATE
     ```

4. **Lister les fichiers logiques** du backup pour connaître les noms logiques data/log (et filestream éventuel) :
   ```sql
   RESTORE FILELISTONLY FROM DISK = '{cheminBak}\{fichier.bak}'
   -- Type='D' → nom logique données, Type='L' → nom logique log, Type='S' → filestream
   ```

5. **Construire les chemins cibles** :
   - `{cheminLdfMdf}\{NomLogiqueDonnee}.mdf`
   - `{cheminLdfMdf}\{NomLogiqueLog}.ldf`
   - `{cheminLdfMdf}\{NomBase}` (dossier filestream, si présent)

6. **Restaurer** :
   ```sql
   RESTORE DATABASE [NomBase]
   FROM DISK = '{cheminBak}\{fichier.bak}'
   WITH REPLACE,
        MOVE '{NomLogiqueDonnee}' TO '{CheminDonnee}',
        MOVE '{NomLogiqueLog}' TO '{CheminLog}'
        [, MOVE '{NomLogiqueFilestream}' TO '{CheminFilestream}']
   ```

7. **Correctif métier spécifique (à rendre optionnel/paramétrable)** : si la base contient une table `F_EPROTECTIONCPTA` (signature d'un logiciel compta type Sage/EBP), remap du login applicatif :
   ```sql
   ALTER AUTHORIZATION ON DATABASE::[NomBase] TO sa
   USE [NomBase]
   ALTER USER [USER_CBASE] WITH LOGIN = [APPL_CBASE]
   ```
   → utile après une restauration cross-serveur où le SID du login diverge (orphan user).

8. **Journaliser** le résultat (restaurée / déjà existante / remplacée / erreur) et incrémenter un compteur `x/N`.

## Points à adapter pour une intégration "toolbox" générique

- **Version SQL Server** : le code adapte légèrement les colonnes `HEADERONLY`/`FILELISTONLY` selon la version (`SnapshotURL`, `KeyAlgorithm`, etc. apparus avec SQL Server 2014+). À garder si vous visez plusieurs versions de moteur.
- **Le correctif `APPL_CBASE`/`USER_CBASE` est spécifique à une appli tierce** — à extraire en callback/plugin optionnel plutôt que codé en dur, si votre toolbox doit rester générique.
- **Auth Windows en dur** — pas de support SQL Auth ; à ajouter si besoin.
- **Pas de restauration différentielle/log** — uniquement restauration complète, un seul fichier `.bak` par base (pas de chaîne full+diff+log).
- **Traitement séquentiel mono-thread** (juste déporté du thread UI) — pas de parallélisation entre bases.
- **Gestion d'erreur par base** : une erreur sur une base n'interrompt pas le lot (continue sur la suivante).

## Interface utilisateur d'origine

- **FormMain** : saisie des 3 chemins + case "Remplacer les bases existantes" + bouton "Restaurer". Les valeurs sont persistées dans `user.config` (settings `.NET`) entre les sessions.
- **FormAvancement** : fenêtre de progression modale, lance le traitement dans un thread séparé, affiche le journal (base par base) et un compteur `x / N base(s) traitée(s)`.
- **FormAPropos** : boîte "À propos" standard (double-clic sur un label `?` dans FormMain).

## Source décompilée

Le code source complet reconstitué (C#, équivalent fonctionnel du binaire VB.NET d'origine) est disponible sur demande — généré via `ilspycmd` à partir de `RestaurationSQL.exe`.

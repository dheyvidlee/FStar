(*
   Copyright 2008-2014 Nikhil Swamy and Microsoft Research

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*)
// (c) Microsoft Corporation. All rights reserved

module Microsoft.FStar.Tc.Env

open Microsoft.FStar 
open Microsoft.FStar.Absyn.Syntax


type binding =
  | Binding_var of bvvdef * typ
  | Binding_typ of btvdef * kind
  | Binding_lid of lident * typ
  | Binding_sig of sigelt

type level = 
  | Expr
  | Type
  | Kind

type env = {
  range:Range.range;             (* the source location of the term being checked *)
  curmodule: lident;             (* Name of this module *)
  gamma:list<binding>;           (* Local typing environment and signature elements *)
  modules:list<modul>;           (* already fully type checked modules *)
  expected_typ:option<typ>;      (* type expected by the context *)
  level:level;                   (* current term being checked is at level *)
  sigtab:Util.smap<sigelt>       (* a dictionary of long-names to sigelts *)
}

val initial_env : lident -> env
val finish_module : env -> modul -> env
val set_level : env -> level -> env
val is_level : env -> level -> bool
val modules : env -> list<modul>
val current_module : env -> lident 
val set_current_module : env -> lident -> env
val set_range : env -> Range.range -> env
val get_range : env -> Range.range

val lookup_bvar : env -> bvvar -> typ
val lookup_lid : env -> lident -> typ      
val try_lookup_val_decl : env -> lident -> option<typ>
val lookup_val_decl : env -> lident -> typ
val lookup_datacon: env -> lident -> typ
val is_datacon : env -> lident -> bool
val is_logic_function : env -> lident -> bool
val is_logic_data : env -> lident -> bool
val is_logic_array : env -> lident -> bool
val is_record : env -> lident -> bool
val lookup_datacons_of_typ : env -> lident -> option<list<(lident * typ)>>
val lookup_typ_abbrev : env -> lident -> option<typ>
val lookup_btvar : env -> btvar -> kind
val lookup_typ_lid : env -> lident -> kind
val lookup_operator : env -> ident -> typ

val push_sigelt : env -> sigelt -> env
val push_local_binding : env -> binding -> env
val uvars_in_env : env -> Absyn.Util.uvars
val push_module : env -> modul -> env

val set_expected_typ : env -> typ -> env
val expected_typ : env -> option<typ>
val clear_expected_typ : env -> env*option<typ>

val fold_env : env -> ('a -> binding -> 'a) -> 'a -> 'a 
val idents : env -> (list<btvdef> * list<bvvdef>) 
val lidents : env -> list<lident>     

(* probably move this to TcUtil *)
val quantifier_pattern_env : env -> typ -> env

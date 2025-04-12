<?php
// Copied from https://github.com/garis-space/adminer-login-servers/blob/c06e18f71a9a69874ef43407c622bc36fdd849ff/adminer/login-servers.php

/**
 * Display servers list from defined ADMINER_SERVERS variable.
 * @link https://www.adminer.org/plugins/#use
 * @author https://github.com/garis-space
*/
class AdminerLoginServers {
  /**
   * Set servers from environment variable
   * Example:
   * $_ENV['ADMINER_SERVERS'] = '{
   *  "Server 1":{"driver":"pgsql","server":"","username":"","password":"","db":""},
   *  "Server 2":{"driver":"pgsql","server":"","username":"","password":"","db":""}
   * }';
  */
  function __construct() {
    $this->servers = array();
    if ($_ENV['ADMINER_SERVERS']) {
      $this->servers = json_decode($_ENV['ADMINER_SERVERS'], true);
    }

    if ($_POST["auth"]["custom_server"]) {
      $key = $_POST["auth"]["custom_server"];
      $_POST["auth"]["driver"] = $this->servers[$key]["driver"];
      $_POST["auth"]["server"] = $this->servers[$key]["server"];
      $_POST["auth"]["username"] = $this->servers[$key]["username"];
      $_POST["auth"]["password"] = $this->servers[$key]["password"];
      $_POST["auth"]["db"] = $this->servers[$key]["db"];
    }
  }

  function loginFormField($name, $heading, $value) {
    if ($name == 'driver') {
      return '<tr><th>Driver<td>' . $value;
    } elseif ($name == 'server') {
      return '<tr><th>Host<td>' . $value;
    } elseif ($name == 'db' && $_ENV['ADMINER_SERVERS'] != '') {
      $out = $heading . $value;
      $out .= '<tr><th><td>or';
      $out .= '<tr><th>Server<td><select name="auth[custom_server]">';
      $out .= '<option value="" selected>--</option>';
      foreach ($this->servers as $serverName => $serverConfig) {
          $out .= '<option value="' . htmlspecialchars($serverName) . '">' . htmlspecialchars($serverName) . '</option>';
      }
      $out .= '</select>';
      return $out;
    }
  }
}

return new AdminerLoginServers();

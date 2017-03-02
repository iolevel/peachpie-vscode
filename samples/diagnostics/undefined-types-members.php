<?php
// Examples of diagnostics of undefined types and members
//
// Currently, only undefined types (in various contexts, as seen below) are reported.
// Dynamic nature of PHP complicates the analysis of undefined properties and methods; therefore,
// it's not implemented at the moment.

// Definitions

function definedFunction($a, $b) {
  return $a + $b;
}

class DefinedClass {
  const CONSTANT = 42;

  static $staticProperty = 42;

  public $property = 42;

  public function method() {
    return 42;
  }

  public static function staticMethod() {
    return 42;
  }
}

class DefinedException extends Exception {}

// Defined entities - diagnostics won't appear

$a = definedFunction(5, 4);
$a = DefinedClass::CONSTANT;
DefinedClass::$staticProperty = DefinedClass::$staticProperty + 1;
$a = DefinedClass::staticMethod();

$goodInstance = new DefinedClass();
$goodInstance->property = $goodInstance->property + 1;
$goodInstance->method();

if ($goodInstance instanceof DefinedClass) {}

try {
} catch (DefinedException $exception) {
}

// Undefined types - diagnostics will appear for Undefined* (and nothing else)

$b = undefinedFunction(5, 4);
$b = UndefinedClass::Constant;
$b = UndefinedClass::$staticProperty;
$b = UndefinedClass::staticMethod();
$b = UndefinedClass::undefinedStaticMethod();

$badInstance = new UndefinedClass();
$badInstance->property = $badInstance->property + 1;
$badInstance->method();

if ($badInstance instanceof UndefinedClass) {}

try {
} catch (UndefinedException $exception) {
}

// Unimplemented - diagnostics might appear in the future

$b = DefinedClass::UndefinedStaticMethod();

$goodInstance->UndefinedMethod();
$goodInstance->undefinedProperty = $goodInstance->undefinedProperty + 1;

$className = "OtherUndefinedClass";
$d = new $className();

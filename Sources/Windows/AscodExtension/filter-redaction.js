function schemaKey(key, index) {
  return /^[a-z_][a-z_0-9.$]{0,63}$/i.test(key) && !/\d{4,}/.test(key) ? key : '[field-' + index + ']';
}

const filterWords = new Set([
  'and', 'or', 'not', 'eq', 'ne', 'lt', 'le', 'gt', 'ge', 'equals', 'greaterthan', 'lessthan',
  'greaterthanorequalto', 'lessthanorequalto', 'between', 'date', 'datetime', 'datetime2', 'convert',
  'cast', 'null', 'true', 'false', 'regdate', 'regdatefrom', 'regdatetill', 'registrationdate',
  'datereg', 'datefrom', 'datetill', 'asc', 'desc', 'is', 'in'
]);
const filterToken = /\/Date\(-?\d+(?:[+-]\d{4})?\)\/|\b\d{4}-\d{2}-\d{2}(?:[T ]\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?|\b\d{1,2}[./]\d{1,2}[./]\d{4}\b|[\p{L}\p{N}_@$]+|[^\s()[\]{}.,:;=<>!+*/%&|?'"`-]+|./gu;

function redactExpression(text, date) {
  const [year, month, day] = date.split('-');
  const dates = new Set([date, day + '.' + month + '.' + year, day + '/' + month + '/' + year,
    month + '/' + day + '/' + year]);
  return (text.slice(0, 12000).match(filterToken) ?? []).map(token => {
    const serializedDate = /^\/Date\((-?\d+)([+-]\d{4})?\)\/$/.exec(token);
    if (serializedDate) {
      const value = new Date(Number(serializedDate[1]));
      const matches = !Number.isNaN(value.getTime()) &&
        (value.toISOString().slice(0, 10) === date || value.toLocaleDateString('en-CA') === date);
      return matches ? token : '/Date(<masked>' + (serializedDate[2] ?? '') + ')/';
    }
    if (dates.has(token.split(/[T ]/)[0]) || filterWords.has(token.toLowerCase()) ||
        /^[\s()[\]{}.,:;=<>!+*/%&|?'"`-]+$/.test(token)) {
      return token;
    }
    return '<masked>';
  }).join('');
}

function redactPayload(value, date, key = '', depth = 0, inFilter = false) {
  if (/token|password|secret|cookie|authorization|session|signature|credential/i.test(key)) {
    return '<masked>';
  }
  const filter = inFilter || key.toLowerCase() === 'filterexpression';
  if (value === null || typeof value === 'boolean') {
    return value;
  }
  if (typeof value === 'number') {
    if (filter && /^_?filterFunction$/i.test(key) && Number.isInteger(value) && value >= 0 && value <= 255) {
      return value;
    }
    return /^(?:startRowIndex|maximumRows|currentPageIndex|pageSize)$/i.test(key) ? value : '<masked-number>';
  }
  if (depth >= 8) {
    return '<truncated>';
  }
  if (typeof value === 'string') {
    if (!filter) {
      return value === '' ? '' : '<masked>';
    }
    if (/^[\s]*[\[{]/.test(value)) {
      try {
        return JSON.stringify(redactPayload(JSON.parse(value), date, key, depth + 1, true));
      } catch {
        return redactExpression(value, date);
      }
    }
    return redactExpression(value, date);
  }
  if (Array.isArray(value)) {
    return value.slice(0, 32).map(child => redactPayload(child, date, key, depth + 1, filter));
  }
  return Object.fromEntries(Object.entries(value).slice(0, 80).map(([name, child], index) =>
    [schemaKey(name, index), redactPayload(child, date, name, depth + 1, filter)]));
}